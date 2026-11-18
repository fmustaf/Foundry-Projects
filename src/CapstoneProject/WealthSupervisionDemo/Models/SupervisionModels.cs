using System;
using System.Collections.Generic;

namespace WealthSupervisionDemo.Models;

public class AdvisorCommunicationEvent
{
    public string MessageId { get; set; } = $"MSG-{Random.Shared.Next(10000, 99999)}";
    public string AdvisorName { get; set; } = "Maya Lin (CRD #482910)";
    public string Channel { get; set; } = "Outlook + Copilot";
    public string EntraAgentId { get; set; } = "entra-agent-wealth-pilot-01";
    public int RetailRecipientCount { get; set; } = 1;
    public bool IsInstitutionalOnly { get; set; } = false;
    public bool HasCopilotProvenance { get; set; } = true;
    public string UserPrompt { get; set; } = string.Empty;
    public string DraftContent { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public record AgentExecutionStep(
    string AgentName,
    string RoleAndAutonomy,
    string ModelUsed,
    long LatencyMs,
    double ConfidenceScore,
    string StatusBadge,
    string Rationale,
    List<string> RegulatoryCitations
);

public class PrincipalReviewItem
{
    public string MessageId { get; set; } = string.Empty;
    public AdvisorCommunicationEvent OriginalEvent { get; set; } = new();
    public string EditedDraftContent { get; set; } = string.Empty;
    public string RuleCategory { get; set; } = "Retail";
    public string ExecutiveSummaryCard { get; set; } = string.Empty;
    public string DecisionState { get; set; } = "PENDING_PRINCIPAL";
    public string PrincipalNotes { get; set; } = string.Empty;
    public DateTimeOffset? ReviewedAtUtc { get; set; }
}

public record Sec17a4AuditRecord(
    string AuditId,
    string MessageId,
    string TraceId,
    DateTimeOffset TimestampUtc,
    string Channel,
    bool IsAiDrafted,
    string RuleClassification,
    string FinalDisposition,
    string RetentionPolicy,
    string FullReasoningPayload
);

public record SupervisionWorkflowResult(
    string MessageId,
    string TraceId,
    string FinalDisposition,
    string RuleClassification,
    bool IsAiDrafted,
    long TotalLatencyMs,
    decimal EstimatedInferenceCostUsd,
    List<AgentExecutionStep> AgentSteps,
    PrincipalReviewItem? ReviewQueueItem,
    Sec17a4AuditRecord AuditRecord
);
