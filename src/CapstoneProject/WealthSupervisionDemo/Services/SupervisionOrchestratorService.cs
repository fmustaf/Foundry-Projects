using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using WealthSupervisionDemo.Models;

namespace WealthSupervisionDemo.Services;

public class SupervisionOrchestratorService
{
    private readonly IConfiguration _config;

    public bool IsMockMode { get; set; }
    public bool RequirePreSendPrincipalApprovalForAll { get; set; } = false;

    public List<PrincipalReviewItem> PrincipalQueue { get; } = new();
    public List<Sec17a4AuditRecord> WormAuditVault { get; } = new();

    public int TotalMessagesProcessed { get; private set; } = 1420;
    public int SurveillanceLoggedCount { get; private set; } = 1318;
    public int HeldForPrincipalCount { get; private set; } = 89;
    public int BlockedDlpCount { get; private set; } = 13;

    public SupervisionOrchestratorService(IConfiguration config)
    {
        _config = config;
        IsMockMode = _config.GetValue<bool>("Foundry:UseMockMode", true);
    }

    public async Task<SupervisionWorkflowResult> ProcessCommunicationAsync(
        AdvisorCommunicationEvent evt,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        string traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")[..16];

        SupervisionWorkflowResult result = IsMockMode
            ? await ExecuteSimulatedPipelineAsync(evt, traceId, sw, ct)
            : await ExecuteLiveFoundryPipelineAsync(evt, traceId, sw, ct);

        TotalMessagesProcessed++;
        if (result.FinalDisposition == "LOGGED_FOR_PRINCIPAL_SURVEILLANCE") SurveillanceLoggedCount++;
        else if (result.FinalDisposition == "HELD_FOR_PRINCIPAL_APPROVAL") HeldForPrincipalCount++;
        else if (result.FinalDisposition == "BLOCKED_DLP") BlockedDlpCount++;

        if (result.ReviewQueueItem != null)
        {
            PrincipalQueue.Insert(0, result.ReviewQueueItem);
        }

        WormAuditVault.Insert(0, result.AuditRecord);
        return result;
    }

    public void ResolvePrincipalReview(string messageId, string action, string editedDraft, string notes)
    {
        var item = PrincipalQueue.FirstOrDefault(x => x.MessageId == messageId);
        if (item == null) return;

        bool wasEdited = !string.IsNullOrWhiteSpace(editedDraft) &&
                         !string.Equals(editedDraft.Trim(), item.OriginalEvent.DraftContent.Trim(), StringComparison.Ordinal);

        if (action == "APPROVE")
        {
            item.DecisionState = wasEdited ? "EDITED_AND_APPROVED_BY_PRINCIPAL" : "APPROVED_BY_PRINCIPAL";
            if (wasEdited) item.EditedDraftContent = editedDraft.Trim();
        }
        else
        {
            item.DecisionState = "REJECTED_BY_PRINCIPAL";
        }

        item.PrincipalNotes = string.IsNullOrWhiteSpace(notes)
            ? $"Registered Principal executed '{item.DecisionState}'."
            : notes;
        item.ReviewedAtUtc = DateTimeOffset.UtcNow;

        WormAuditVault.Insert(0, new Sec17a4AuditRecord(
            AuditId: $"WORM-{Random.Shared.Next(100000, 999999)}",
            MessageId: messageId,
            TraceId: Guid.NewGuid().ToString("N")[..16],
            TimestampUtc: DateTimeOffset.UtcNow,
            Channel: item.OriginalEvent.Channel,
            IsAiDrafted: item.OriginalEvent.HasCopilotProvenance,
            RuleClassification: item.RuleCategory,
            FinalDisposition: item.DecisionState,
            RetentionPolicy: "SEC 17a-4(b)(4) - 6-Year WORM + Purview eDiscovery Hold",
            FullReasoningPayload: JsonSerializer.Serialize(new
            {
                PrincipalAction = item.DecisionState,
                OriginalDraft = item.OriginalEvent.DraftContent,
                FinalApprovedDraft = wasEdited ? item.EditedDraftContent : item.OriginalEvent.DraftContent,
                PrincipalRationale = item.PrincipalNotes
            }, new JsonSerializerOptions { WriteIndented = true })
        ));
    }

    private async Task<SupervisionWorkflowResult> ExecuteLiveFoundryPipelineAsync(
        AdvisorCommunicationEvent evt,
        string traceId,
        Stopwatch sw,
        CancellationToken ct)
    {
        string endpointStr = _config["Foundry:ProjectEndpoint"]
            ?? throw new InvalidOperationException("Foundry:ProjectEndpoint is not configured.");
        if (!string.IsNullOrWhiteSpace(_config["Foundry:ApiKey"]))
        {
            throw new InvalidOperationException(
                "Foundry:ApiKey is not supported by Azure.AI.Projects 2.x. Configure Microsoft Entra ID authentication instead.");
        }

        AIProjectClient foundryClient = new(new Uri(endpointStr), new DefaultAzureCredential());

        // AGENT 1: Supervision & Governance Sentinel (L4 Supervisor)
        AIAgent governanceSentinel = foundryClient.AsAIAgent(
            model: "gpt-4.1",
            name: "GovernanceSentinelAgent",
            instructions: "You are Agent 1: Supervision & Governance Sentinel (L4 Supervisor). Invariant: AI agents NEVER approve communications; Registered Principals hold 100% approval authority. Verify EntraAgentId if Channel is Copilot Studio, and check for unmasked account numbers or SSNs. If DLP violation or invalid identity is found, start response with 'DLP_BLOCK:'. Otherwise start with 'DLP_PASS:'.");

        // AGENT 2: Communication Classifier (L4 Specialist)
        AIAgent classifierAgent = foundryClient.AsAIAgent(
            model: "gpt-4.1-mini",
            name: "CommunicationClassifier",
            instructions: "You are Agent 2: Communication Classifier (L4). Classify under FINRA Rule 2210: 'Retail' if RetailRecipientCount > 25 or Channel contains 'Copilot Studio', 'Institutional' if IsInstitutionalOnly is true, 'Correspondence' if RetailRecipientCount <= 25. Flag AI provenance if HasCopilotProvenance is true. Respond ONLY with JSON: {\"Category\":\"Retail|Correspondence|Institutional\",\"Confidence\":0.98,\"Rationale\":\"...\"}");

        // AGENT 3: Content Standards Reviewer (L2 Specialist)
        AIAgent contentReviewer = foundryClient.AsAIAgent(
            model: "gpt-4.1",
            name: "ContentStandardsReviewer",
            instructions: "You are Agent 3: Content Standards Reviewer (L2). Evaluate DraftContent under FINRA Rule 2210 and Regulatory Notice 24-09 for promissory claims ('guaranteed', 'risk-free', '12% return'), exaggerated projections, or missing risk disclosures. Ignore benign scheduling phrases ('I guarantee I will send the PDF tomorrow'). Respond ONLY with JSON: {\"Violation\":true|false,\"Confidence\":0.96,\"Rationale\":\"...\"}");

        // AGENT 4: Off-Channel Sentinel (L2 Specialist)
        AIAgent offChannelSentinel = foundryClient.AsAIAgent(
            model: "gpt-4.1-mini",
            name: "OffChannelSentinel",
            instructions: "You are Agent 4: Off-Channel Sentinel (L2). Inspect DraftContent under FINRA Rule 3110 for attempts to move business discussions to unapproved channels (WhatsApp, text my cell, Signal, Telegram, personal email). Respond ONLY with JSON: {\"OffChannelFlag\":true|false,\"Confidence\":0.97,\"Rationale\":\"...\"}");

        // AGENT 5: Pre-Approval Router (L3 HITL Gate)
        AIAgent preApprovalRouter = foundryClient.AsAIAgent(
            model: "gpt-4.1-mini",
            name: "PreApprovalRouter",
            instructions: "You are Agent 5: Pre-Approval Router (L3). Produce a 3-bullet Microsoft Teams Adaptive Card brief for a Registered Principal covering Rule Classification & AI Provenance, Risk Triggers & Quoted Evidence, and Recommended Principal Action (Approve / Edit / Reject).");

        // AGENT 6: Records Keeper (L4 Compliance Archiver)
        AIAgent recordsKeeper = foundryClient.AsAIAgent(
            model: "gpt-4.1-mini",
            name: "RecordsKeeper",
            instructions: "You are Agent 6: Records Keeper (L4). Confirm SEC Rule 17a-4(b)(4) WORM archival and Purview eDiscovery Premium hold for the prompt, draft, agent verdicts, and disposition.");

        var steps = new List<AgentExecutionStep>();
        string payloadJson = JsonSerializer.Serialize(evt);

        // STEP 1A: Governance Sentinel Intake
        var stepSw = Stopwatch.StartNew();
        var intakeRes = await governanceSentinel.RunAsync(
            $"Validate identity and Purview DLP: {payloadJson}", session: null, cancellationToken: ct);
        stepSw.Stop();

        bool dlpBlocked = intakeRes.Text.StartsWith("DLP_BLOCK", StringComparison.OrdinalIgnoreCase);
        steps.Add(new AgentExecutionStep(
            "1A. Governance Sentinel (Intake)", "L4 Supervisor", "gpt-4.1",
            stepSw.ElapsedMilliseconds, 0.99, dlpBlocked ? "BLOCKED" : "PASS",
            intakeRes.Text, new List<string> { "Purview DLP", "Entra Agent ID" }));

        if (dlpBlocked)
        {
            var archRes = await recordsKeeper.RunAsync(
                $"Archive DLP Block Trace {traceId}: {payloadJson}", session: null, cancellationToken: ct);
            steps.Add(new AgentExecutionStep(
                "6. Records Keeper", "L4 Archiver", "gpt-4.1-mini",
                140, 1.00, "ARCHIVED", archRes.Text, new List<string> { "SEC Rule 17a-4" }));

            sw.Stop();
            var dlpAudit = BuildAuditRecord(evt, traceId, "Blocked", "BLOCKED_DLP", steps);
            return new SupervisionWorkflowResult(
                evt.MessageId, traceId, "BLOCKED_DLP", "Blocked", evt.HasCopilotProvenance,
                sw.ElapsedMilliseconds, 0.0021m, steps, null, dlpAudit);
        }

        // STEP 2: Concurrent Fan-Out across Agents 2, 3, and 4
        stepSw.Restart();
        var concurrentWorkflow = AgentWorkflowBuilder.BuildConcurrent(
            agents: [classifierAgent, contentReviewer, offChannelSentinel]);
        List<ChatMessage> workflowInput = [new(ChatRole.User, payloadJson)];
        var concurrentRun = await InProcessExecution.RunAsync(
            concurrentWorkflow, workflowInput, cancellationToken: ct);
        stepSw.Stop();

        string rawFindings = string.Join(
            Environment.NewLine,
            concurrentRun.OutgoingEvents
                .OfType<WorkflowOutputEvent>()
                .Select(output => output.As<List<ChatMessage>>())
                .SelectMany(messages => messages)
                .Select(message => message.Text));
        string ruleCategory = (evt.RetailRecipientCount > 25 || evt.Channel.Contains("Copilot Studio"))
            ? "Retail"
            : (evt.IsInstitutionalOnly ? "Institutional" : "Correspondence");

        bool contentViolation = rawFindings.Contains("\"Violation\":true", StringComparison.OrdinalIgnoreCase) ||
                                rawFindings.Contains("\"Violation\": true", StringComparison.OrdinalIgnoreCase);
        bool offChannelFlag = rawFindings.Contains("\"OffChannelFlag\":true", StringComparison.OrdinalIgnoreCase) ||
                              rawFindings.Contains("\"OffChannelFlag\": true", StringComparison.OrdinalIgnoreCase);

        double minSpecialistConfidence = ExtractMinimumConfidence(rawFindings, defaultConfidence: 0.95);
        bool lowConfidenceFailClosed = minSpecialistConfidence < 0.85;

        steps.Add(new AgentExecutionStep(
            "2. Communication Classifier", "L4 Specialist (Concurrent)", "gpt-4.1-mini",
            stepSw.ElapsedMilliseconds, 0.96, ruleCategory == "Retail" ? "FLAGGED" : "PASS",
            $"FINRA 2210 Category: {ruleCategory} | AI-Drafted Provenance: {evt.HasCopilotProvenance}.",
            new List<string> { "FINRA Rule 2210", "Notice 24-09" }));

        steps.Add(new AgentExecutionStep(
            "3. Content Standards Reviewer", "L2 Specialist (Concurrent)", "gpt-4.1",
            stepSw.ElapsedMilliseconds, 0.94, contentViolation ? "FLAGGED" : "PASS",
            rawFindings, new List<string> { "FINRA Rule 2210(d)", "Notice 24-09" }));

        steps.Add(new AgentExecutionStep(
            "4. Off-Channel Sentinel", "L2 Specialist (Concurrent)", "gpt-4.1-mini",
            stepSw.ElapsedMilliseconds, 0.95, offChannelFlag ? "FLAGGED" : "PASS",
            offChannelFlag ? "Off-channel messaging solicitation detected." : "No unapproved channel migration detected.",
            new List<string> { "FINRA Rule 3110" }));

        // STEP 1B: Governance Sentinel Policy Synthesis & Fail-Closed Gate
        bool requiresPreSendPrincipal =
            RequirePreSendPrincipalApprovalForAll ||
            ruleCategory == "Retail" ||
            contentViolation ||
            offChannelFlag ||
            lowConfidenceFailClosed;

        string finalDisposition = requiresPreSendPrincipal
            ? "HELD_FOR_PRINCIPAL_APPROVAL"
            : "LOGGED_FOR_PRINCIPAL_SURVEILLANCE";

        steps.Add(new AgentExecutionStep(
            "1B. Governance Sentinel (Policy Gate)", "L4 Fail-Closed Gate", "Code-Enforced",
            10, 1.00, requiresPreSendPrincipal ? "FLAGGED" : "PASS",
            requiresPreSendPrincipal
                ? $"Zero Agent Approval Authority enforced (MinConf={minSpecialistConfidence:P0}): Hard-locking draft for Registered Principal sign-off in Teams."
                : "Clean 1-to-1 Correspondence: Agents never approve; logged to Registered Principal FINRA 3110 post-use surveillance queue.",
            new List<string> { "FINRA Rule 2210", "FINRA Rule 3110" }));

        PrincipalReviewItem? reviewItem = null;

        // STEP 3: Conditional Route to Agent 5 (Pre-Approval Router HITL Gate)
        if (requiresPreSendPrincipal)
        {
            stepSw.Restart();
            var routerRes = await preApprovalRouter.RunAsync(
                $"Build Teams Adaptive Card brief from: {rawFindings}", session: null, cancellationToken: ct);
            stepSw.Stop();

            steps.Add(new AgentExecutionStep(
                "5. Pre-Approval Router", "L3 HITL Gate", "gpt-4.1-mini",
                stepSw.ElapsedMilliseconds, 0.98, "FLAGGED",
                routerRes.Text, new List<string> { "FINRA Rule 2210(b)", "FINRA Rule 3110" }));

            reviewItem = new PrincipalReviewItem
            {
                MessageId = evt.MessageId,
                OriginalEvent = evt,
                EditedDraftContent = evt.DraftContent,
                RuleCategory = ruleCategory,
                ExecutiveSummaryCard = routerRes.Text
            };
        }

        // STEP 4: Mandatory Agent 6 (Records Keeper) SEC 17a-4 WORM Archival
        stepSw.Restart();
        var wormRes = await recordsKeeper.RunAsync(
            $"Commit 17a-4 WORM record for {evt.MessageId} ({finalDisposition})", session: null, cancellationToken: ct);
        stepSw.Stop();

        steps.Add(new AgentExecutionStep(
            "6. Records Keeper", "L4 Archiver", "gpt-4.1-mini",
            stepSw.ElapsedMilliseconds, 1.00, "ARCHIVED",
            wormRes.Text, new List<string> { "SEC Rule 17a-4(b)(4)", "Purview eDiscovery" }));

        sw.Stop();
        var audit = BuildAuditRecord(evt, traceId, ruleCategory, finalDisposition, steps);
        return new SupervisionWorkflowResult(
            evt.MessageId, traceId, finalDisposition, ruleCategory, evt.HasCopilotProvenance,
            sw.ElapsedMilliseconds, 0.0042m, steps, reviewItem, audit);
    }

    private async Task<SupervisionWorkflowResult> ExecuteSimulatedPipelineAsync(
        AdvisorCommunicationEvent evt,
        string traceId,
        Stopwatch sw,
        CancellationToken ct)
    {
        await Task.Delay(550, ct);

        var steps = new List<AgentExecutionStep>();
        string text = evt.DraftContent ?? string.Empty;

        bool hasAccountNumber = Regex.IsMatch(text, @"\b(ACCT|ACC|ACCOUNT)[-\s#]*\d{5,12}\b|\b\d{3}-\d{2}-\d{4}\b", RegexOptions.IgnoreCase);
        if (hasAccountNumber)
        {
            steps.Add(new AgentExecutionStep(
                "1A. Governance Sentinel (Intake)", "L4 Policy & DLP Gate", "gpt-4.1",
                130, 0.99, "BLOCKED",
                "DLP_BLOCK: Unmasked client account number/SSN detected by Purview DLP perimeter. Outbound transmission blocked immediately.",
                new List<string> { "Purview DLP Policy #FIN-01", "Entra Agent ID Verified" }));

            steps.Add(new AgentExecutionStep(
                "6. Records Keeper", "L4 Compliance Archiver", "gpt-4.1-mini",
                90, 1.00, "ARCHIVED",
                $"Blocked DLP event committed to SEC Rule 17a-4 WORM Vault with Purview eDiscovery Hold (Trace: {traceId}).",
                new List<string> { "SEC Rule 17a-4(b)(4)", "eDiscovery Premium" }));

            sw.Stop();
            var blockedAudit = BuildAuditRecord(evt, traceId, "Blocked", "BLOCKED_DLP", steps);
            return new SupervisionWorkflowResult(
                evt.MessageId, traceId, "BLOCKED_DLP", "Blocked", evt.HasCopilotProvenance,
                sw.ElapsedMilliseconds, 0.0018m, steps, null, blockedAudit);
        }

        steps.Add(new AgentExecutionStep(
            "1A. Governance Sentinel (Intake)", "L4 Policy & DLP Gate", "gpt-4.1",
            125, 0.99, "PASS",
            $"Entra Agent ID verified ({evt.EntraAgentId}). Zero unmasked account numbers/SSNs detected. Fanning out to Agents 2, 3, and 4.",
            new List<string> { "Entra Agent ID", "Purview DLP" }));

        string ruleCategory = (evt.RetailRecipientCount > 25 || evt.Channel.Contains("Copilot Studio"))
            ? "Retail"
            : (evt.IsInstitutionalOnly ? "Institutional" : "Correspondence");

        double classifierConf = 0.98;
        steps.Add(new AgentExecutionStep(
            "2. Communication Classifier", "L4 Specialist (Concurrent)", "gpt-4.1-mini",
            280, classifierConf, ruleCategory == "Retail" ? "FLAGGED" : "PASS",
            $"Classified as '{ruleCategory}' (Recipients: {evt.RetailRecipientCount}, Channel: {evt.Channel}). AI-Drafted Provenance: {evt.HasCopilotProvenance}.",
            new List<string> { "FINRA Rule 2210(a)", "FINRA Notice 24-09" }));

        bool isBenignGuarantee = Regex.IsMatch(text, @"guarantee\s+(i('ll| will)|we('ll| will))\s+(send|email|call|follow|get)", RegexOptions.IgnoreCase);
        bool hasPromissoryRisk = !isBenignGuarantee && Regex.IsMatch(
            text,
            @"(guaranteed\s+return|risk[-\s]?free|12%\s+yield|cannot\s+lose|beat\s+the\s+market\s+every)",
            RegexOptions.IgnoreCase);

        bool isAmbiguous = Regex.IsMatch(text, @"\b(might\s+outperform|historically\s+always\s+up)\b", RegexOptions.IgnoreCase);
        double reviewerConf = isAmbiguous ? 0.78 : 0.96;

        string contentRationale = hasPromissoryRisk
            ? "VIOLATION: Promissory performance claim ('guaranteed/risk-free yield') detected without balanced principal-loss disclosure."
            : isAmbiguous
                ? "AMBIGUOUS (Confidence 78% < 85% threshold): Subtle performance implication triggers Fail-Closed escalation to Principal."
                : isBenignGuarantee
                    ? "CLEAN (False-Positive Suppressed): Benign scheduling phrase ('guarantee I'll send') distinguished from investment promise."
                    : "CLEAN: Balanced market commentary; risk disclosures verified.";

        steps.Add(new AgentExecutionStep(
            "3. Content Standards Reviewer", "L2 Specialist (Concurrent)", "gpt-4.1",
            440, reviewerConf, (hasPromissoryRisk || isAmbiguous) ? "FLAGGED" : "PASS",
            contentRationale,
            new List<string> { "FINRA Rule 2210(d)(1)", "FINRA Notice 24-09" }));

        bool hasOffChannelHint = Regex.IsMatch(
            text,
            @"(whatsapp|text\s+my\s+cell|personal\s+phone|signal\s+app|telegram|my\s+gmail)",
            RegexOptions.IgnoreCase);

        double offChannelConf = 0.97;
        steps.Add(new AgentExecutionStep(
            "4. Off-Channel Sentinel", "L2 Specialist (Concurrent)", "gpt-4.1-mini",
            260, offChannelConf, hasOffChannelHint ? "FLAGGED" : "PASS",
            hasOffChannelHint
                ? "OFF_CHANNEL_FLAG: Detected solicitation to move client communication to unapproved app (WhatsApp/SMS)."
                : "CLEAN: All communication remains within supervised M365 boundaries.",
            new List<string> { "FINRA Rule 3110", "SEC Rule 17a-4" }));

        double minConfidence = new[] { classifierConf, reviewerConf, offChannelConf }.Min();
        bool lowConfidenceFailClosed = minConfidence < 0.85;

        bool requiresPreSendPrincipal =
            RequirePreSendPrincipalApprovalForAll ||
            ruleCategory == "Retail" ||
            hasPromissoryRisk ||
            hasOffChannelHint ||
            lowConfidenceFailClosed;

        string finalDisposition = requiresPreSendPrincipal
            ? "HELD_FOR_PRINCIPAL_APPROVAL"
            : "LOGGED_FOR_PRINCIPAL_SURVEILLANCE";

        steps.Add(new AgentExecutionStep(
            "1B. Governance Sentinel (Policy Gate)", "L4 Fail-Closed Gate", "Code-Enforced",
            8, 1.00, requiresPreSendPrincipal ? "FLAGGED" : "PASS",
            requiresPreSendPrincipal
                ? $"Zero Agent Approval Authority enforced (MinConf={minConfidence:P0}): Draft hard-locked pending Registered Principal approval in Teams."
                : "Clean 1-to-1 Correspondence: Agents never approve; routed to Registered Principal FINRA 3110 post-use surveillance log.",
            new List<string> { "FINRA Rule 2210", "FINRA Rule 3110" }));

        PrincipalReviewItem? reviewItem = null;

        if (requiresPreSendPrincipal)
        {
            var reasons = new List<string>();
            if (RequirePreSendPrincipalApprovalForAll && ruleCategory != "Retail" && !hasPromissoryRisk && !hasOffChannelHint && !lowConfidenceFailClosed)
                reasons.Add("Strict Firm WSP Policy active: 100% of communications require pre-send Principal sign-off.");
            if (ruleCategory == "Retail")
                reasons.Add("FINRA Rule 2210 Retail Communication (>25 recipients / Client Agent) requires pre-use Principal approval.");
            if (hasPromissoryRisk)
                reasons.Add("Promissory performance claim detected without risk disclosure.");
            if (hasOffChannelHint)
                reasons.Add("Off-channel messaging attempt (WhatsApp/SMS) flagged under FINRA Rule 3110.");
            if (lowConfidenceFailClosed)
                reasons.Add($"Fail-Closed Escalation: Specialist confidence ({minConfidence:P0}) fell below 85% safety threshold.");

            string teamsBrief = $"• Classification & Provenance: {ruleCategory} | AI-Drafted: {evt.HasCopilotProvenance}\n" +
                                $"• Risk Triggers: {string.Join(" ", reasons)}\n" +
                                $"• Principal Action Required: Review, Edit, Approve, or Reject in Teams (Agents hold zero approval authority).";

            steps.Add(new AgentExecutionStep(
                "5. Pre-Approval Router", "L3 HITL Workflow Gate", "gpt-4.1-mini",
                195, 0.99, "FLAGGED",
                "Draft locked in pending state. Dispatched 3-bullet Adaptive Card to Registered Principal queue in Microsoft Teams.",
                new List<string> { "FINRA Rule 2210(b)(1)", "FINRA Rule 3110" }));

            reviewItem = new PrincipalReviewItem
            {
                MessageId = evt.MessageId,
                OriginalEvent = evt,
                EditedDraftContent = evt.DraftContent,
                RuleCategory = ruleCategory,
                ExecutiveSummaryCard = teamsBrief
            };
        }

        steps.Add(new AgentExecutionStep(
            "6. Records Keeper", "L4 Compliance Archiver", "gpt-4.1-mini",
            105, 1.00, "ARCHIVED",
            $"Committed prompt, draft, 6-agent reasoning chain, and disposition ({finalDisposition}) to SharePoint WORM library with Purview eDiscovery Hold.",
            new List<string> { "SEC Rule 17a-4(b)(4)", "SEC Rule 17a-4(f)" }));

        sw.Stop();
        var audit = BuildAuditRecord(evt, traceId, ruleCategory, finalDisposition, steps);
        return new SupervisionWorkflowResult(
            evt.MessageId, traceId, finalDisposition, ruleCategory, evt.HasCopilotProvenance,
            sw.ElapsedMilliseconds, 0.0041m, steps, reviewItem, audit);
    }

    private static double ExtractMinimumConfidence(string jsonBlob, double defaultConfidence)
    {
        var matches = Regex.Matches(jsonBlob, "\"Confidence\"\\s*:\\s*(0\\.\\d+|1\\.0)");
        if (matches.Count == 0) return defaultConfidence;
        return matches.Select(m => double.Parse(m.Groups[1].Value)).Min();
    }

    private static Sec17a4AuditRecord BuildAuditRecord(
        AdvisorCommunicationEvent evt,
        string traceId,
        string ruleCategory,
        string disposition,
        List<AgentExecutionStep> steps)
    {
        return new Sec17a4AuditRecord(
            AuditId: $"WORM-{Random.Shared.Next(100000, 999999)}",
            MessageId: evt.MessageId,
            TraceId: traceId,
            TimestampUtc: DateTimeOffset.UtcNow,
            Channel: evt.Channel,
            IsAiDrafted: evt.HasCopilotProvenance,
            RuleClassification: ruleCategory,
            FinalDisposition: disposition,
            RetentionPolicy: "SEC 17a-4(b)(4) - 6-Year Non-Rewriteable WORM + Purview eDiscovery Hold",
            FullReasoningPayload: JsonSerializer.Serialize(new
            {
                GovernanceInvariant = "ZERO_AGENT_APPROVAL_AUTHORITY",
                Prompt = evt.UserPrompt,
                Draft = evt.DraftContent,
                AgentChain = steps.Select(s => new { s.AgentName, s.StatusBadge, s.ConfidenceScore, s.Rationale })
            }, new JsonSerializerOptions { WriteIndented = true })
        );
    }
}
