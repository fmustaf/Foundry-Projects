# Learning Microsoft Agent Framework Through a Multi-Turn Conversation

## Purpose of this guide

This document teaches the solution in this folder as a small but complete Microsoft Agent Framework example. It explains:

- What each solution file contributes.
- How an Azure OpenAI chat model becomes an Agent Framework agent.
- How `AgentSession` carries conversation history across turns.
- The difference between complete and streaming responses.
- How Microsoft Entra ID authentication works in the sample.
- What every direct and transitive NuGet dependency contributes.
- How to configure, build, and run the application.
- Which parts of the current code are useful for teaching and which parts should be improved before production use.

The review was performed on October 6, 2026. The project builds successfully with zero warnings and zero errors. The configured package feeds reported no known vulnerable or deprecated packages.

> [!IMPORTANT]
> The current `Program.cs` compiles, but it contains several teaching-stage inconsistencies. Read the **Code review findings** section before using it as a production template.

---

## 1. What the solution demonstrates

The application creates an agent named `AgentBond` that is instructed to tell jokes. It sends prompts to a deployed Azure OpenAI chat model and uses an `AgentSession` to retain conversation history.

The important object flow is:

```text
AzureOpenAIClient
    -> OpenAI ChatClient
    -> IChatClient
    -> AIAgent
    -> AgentSession
    -> RunAsync / RunStreamingAsync
```

This layering is the central lesson:

1. **Azure OpenAI provides the model endpoint.**
2. **The Azure OpenAI SDK creates a provider-specific chat client.**
3. **Microsoft.Extensions.AI exposes that client through `IChatClient`.**
4. **Microsoft Agent Framework turns the chat client into an `AIAgent`.**
5. **An `AgentSession` supplies conversation state.**
6. **The application chooses complete or streaming execution.**

Agent Framework does not replace the model provider. It gives the application a consistent agent programming model on top of that provider.

---

## 2. Solution inventory

### Authored files

| File | Purpose |
| --- | --- |
| `QuickStart_Agent_Framework_Multi_Turn_Convo.sln` | Visual Studio solution containing the console project. |
| `QuickStart_Agent_Framework_Multi_Turn_Convo.csproj` | Defines the .NET target, compiler features, and direct NuGet dependencies. |
| `Program.cs` | Configures authentication, creates the model client and agent, creates a session, and invokes the agent. |
| `Agent Framework HOL.md` | A hands-on lab with Azure setup, code exercises, troubleshooting, and knowledge checks. |
| `.github/copilot-instructions.md` | Repository-specific guidance to use Azure tools and Azure best practices. |
| `LearningAgentFrameworkMultiConvo.md` | This code review and student learning guide. |

### Generated folders

| Folder | Purpose |
| --- | --- |
| `bin\` | Compiled executables, assemblies, symbols, runtime configuration, and copied dependency assemblies. |
| `obj\` | Intermediate compiler and NuGet restore output. |
| `.vs\` | Local Visual Studio workspace data. |

The generated folders are not application source. Do not learn the architecture by reading copied DLL names under `bin\`; use the project file and NuGet package inventory instead.

---

## 3. Project configuration

The project file contains:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

### What these settings mean

| Setting | Meaning |
| --- | --- |
| `OutputType=Exe` | Build a runnable console application. |
| `TargetFramework=net10.0` | Compile against .NET 10. The .NET 10 SDK must be installed. |
| `ImplicitUsings=enable` | The SDK automatically imports common namespaces such as `System`. |
| `Nullable=enable` | The compiler performs nullable reference-type analysis. |

Nullable analysis is particularly helpful in AI applications because configuration commonly comes from environment variables and may be missing. A pattern such as the following converts a nullable configuration value into an explicit startup failure:

```csharp
string endpoint =
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException(
        "Set the AZURE_OPENAI_ENDPOINT environment variable.");
```

---

## 4. Direct dependency review

The project currently references four NuGet packages:

| Package | Requested and resolved version | Responsibility in this solution |
| --- | ---: | --- |
| `Azure.AI.OpenAI` | `2.1.0` | Provides `AzureOpenAIClient` and access to the deployed Azure OpenAI chat model. |
| `Azure.Identity` | `1.21.0` | Provides `DefaultAzureCredential` and other Microsoft Entra ID credential implementations. |
| `Microsoft.Agents.AI` | `1.3.0` | Provides Agent Framework types such as `AIAgent`, `AgentSession`, `RunAsync`, and `RunStreamingAsync`. |
| `Microsoft.Extensions.AI.OpenAI` | `10.5.0` | Supplies adapters that expose the OpenAI chat client through the common `IChatClient` abstraction. |

### Package-version status

The package check against the configured feeds reported:

| Package | Current | Newer version reported on October 6, 2026 |
| --- | ---: | ---: |
| `Azure.AI.OpenAI` | `2.1.0` | No newer version reported |
| `Azure.Identity` | `1.21.0` | No newer version reported |
| `Microsoft.Agents.AI` | `1.3.0` | `1.23.0` |
| `Microsoft.Extensions.AI.OpenAI` | `10.5.0` | `10.10.1` |

The two Agent Framework-related packages are behind the versions available from the configured feeds. Do not update only one of them casually. Agent Framework and Microsoft.Extensions.AI APIs have evolved, and current Microsoft documentation uses newer package combinations, including `Microsoft.Agents.AI.OpenAI` for the Azure OpenAI provider.

For this training solution, keeping the pinned versions preserves the code shown in the lab. For a new production solution:

1. Read the current Agent Framework upgrade guidance.
2. Update related packages together.
3. Replace obsolete adapters or namespaces as required.
4. Rebuild and run multi-turn and streaming tests.
5. Confirm authentication and tool behavior against the deployed model.

### Why `IChatClient` matters

`IChatClient` comes from Microsoft.Extensions.AI. It gives Agent Framework a provider-neutral chat interface. The current code performs this adaptation:

```csharp
var chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

The stages are:

1. `AzureOpenAIClient` knows how to communicate with Azure OpenAI.
2. `GetChatClient` selects a deployed chat model.
3. `AsIChatClient` adapts the provider-specific client.
4. `AsAIAgent` adds Agent Framework behavior.

This separation makes it easier to change providers or insert middleware without rewriting the application around a provider-specific type.

---

## 5. Complete transitive dependency inventory

Transitive dependencies are packages brought in by the four direct references. The application does not reference them explicitly, but the direct packages need them at runtime or build time.

| Transitive package | Resolved version | Role |
| --- | ---: | --- |
| `Azure.Core` | `1.53.0` | Shared Azure SDK pipeline, credentials, diagnostics, retries, and HTTP abstractions. |
| `Google.Protobuf` | `3.30.2` | Protocol Buffers serialization used by a dependency. |
| `Microsoft.Agents.AI.Abstractions` | `1.3.0` | Core Agent Framework contracts and shared agent types. |
| `Microsoft.Bcl.AsyncInterfaces` | `10.0.3` | Compatibility support for asynchronous interfaces. |
| `Microsoft.Extensions.AI` | `10.5.0` | Implementations and extensions around the Microsoft.Extensions.AI abstractions. |
| `Microsoft.Extensions.AI.Abstractions` | `10.5.0` | Provider-neutral AI interfaces such as `IChatClient`. |
| `Microsoft.Extensions.AI.Evaluation` | `10.4.0` | Evaluation-related infrastructure included by the dependency graph. |
| `Microsoft.Extensions.Caching.Abstractions` | `10.0.6` | Common cache contracts. |
| `Microsoft.Extensions.Compliance.Abstractions` | `10.5.0` | Compliance classification and data-handling abstractions. |
| `Microsoft.Extensions.Configuration.Abstractions` | `10.0.3` | Common configuration contracts. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.6` | Dependency injection contracts. |
| `Microsoft.Extensions.Diagnostics.Abstractions` | `10.0.3` | Shared diagnostics contracts. |
| `Microsoft.Extensions.FileProviders.Abstractions` | `10.0.3` | File-provider contracts used by Microsoft.Extensions components. |
| `Microsoft.Extensions.Hosting.Abstractions` | `10.0.3` | Host lifecycle and environment contracts. |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.6` | Logging contracts used throughout the stack. |
| `Microsoft.Extensions.ObjectPool` | `10.0.6` | Reusable object-pooling infrastructure. |
| `Microsoft.Extensions.Options` | `10.0.3` | Strongly typed options and configuration patterns. |
| `Microsoft.Extensions.Primitives` | `10.0.6` | Shared primitives used by Microsoft.Extensions libraries. |
| `Microsoft.Extensions.VectorData.Abstractions` | `9.7.0` | Provider-neutral vector data contracts included by the AI stack. |
| `Microsoft.Identity.Client` | `4.83.1` | MSAL authentication implementation for Microsoft Entra ID. |
| `Microsoft.Identity.Client.Extensions.Msal` | `4.83.1` | MSAL token-cache extensions. |
| `Microsoft.IdentityModel.Abstractions` | `8.14.0` | Shared Microsoft identity model contracts. |
| `Microsoft.ML.Tokenizers` | `2.0.0` | Tokenization utilities used by AI-related dependencies. |
| `OpenAI` | `2.10.0` | The underlying .NET OpenAI client types, including chat APIs. |
| `System.ClientModel` | `1.10.0` | Common client-model pipeline used by newer service SDKs. |
| `System.Memory.Data` | `10.0.3` | Binary and memory-oriented data support. |
| `System.Numerics.Tensors` | `10.0.6` | Tensor primitives used by AI-related libraries. |
| `System.Security.Cryptography.ProtectedData` | `4.5.0` | Windows protected-data support used by identity/token caching. |

### How to interpret this list

You normally manage only direct dependencies. NuGet resolves compatible transitive versions for you. Add a transitive package as a direct reference only when the application directly uses its API or must deliberately control its version.

The update check reported newer versions for many transitive packages. Updating the two older top-level Agent Framework packages is the correct way to let NuGet recalculate most of those versions. Manually upgrading every transitive dependency would make the project harder to maintain and could create incompatible combinations.

### Dependency health result

The following commands were used:

```powershell
dotnet list .\QuickStart_Agent_Framework_Multi_Turn_Convo.csproj package --include-transitive
dotnet list .\QuickStart_Agent_Framework_Multi_Turn_Convo.csproj package --outdated --include-transitive
dotnet list .\QuickStart_Agent_Framework_Multi_Turn_Convo.csproj package --vulnerable --include-transitive
dotnet list .\QuickStart_Agent_Framework_Multi_Turn_Convo.csproj package --deprecated --include-transitive
```

Results:

- No known vulnerable packages were reported by the configured sources.
- No deprecated packages were reported.
- Newer Agent Framework and Microsoft.Extensions.AI packages are available.
- The current package set restores and builds successfully.

Package vulnerability data changes over time. Run these checks regularly and in CI.

---

## 6. Walking through `Program.cs`

### 6.1 Namespace imports

The file begins with:

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel.Primitives;
```

The first four namespaces are relevant to the current implementation:

- `Azure.AI.OpenAI` supplies `AzureOpenAIClient`.
- `Azure.Identity` supplies `DefaultAzureCredential`.
- `Microsoft.Agents.AI` supplies agent and session APIs.
- `Microsoft.Extensions.AI` supplies `IChatClient` integration and adapters.

`OpenAI`, `OpenAI.Chat`, and `System.ClientModel.Primitives` are not directly used by the current source and can be removed unless a future exercise uses their types.

### 6.2 Model deployment and endpoint

The current code uses:

```csharp
const string deploymentName = "gpt-4.1-mini";
const string endpoint =
    "https://demofaisalnewui-1423-resource.openai.azure.com/";
```

The deployment name identifies the Azure OpenAI deployment, not just the model family. A deployment could use the `gpt-4.1-mini` model but have a different deployment name such as `training-chat`.

The endpoint is an **Azure OpenAI resource endpoint**. That is the correct endpoint type for `AzureOpenAIClient`.

Do not substitute a Microsoft Foundry project endpoint such as:

```text
https://<resource>.services.ai.azure.com/api/projects/<project>
```

for this constructor. A Foundry project endpoint is used by project-oriented clients, not by `AzureOpenAIClient`.

Although an endpoint is not normally a secret, hard-coding it couples the source to one resource. Prefer:

```csharp
string endpoint =
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException(
        "Set AZURE_OPENAI_ENDPOINT.");
```

### 6.3 API key variable

The code reads:

```csharp
var apiKey = Environment.GetEnvironmentVariable(
        "AZURE_API_KEY_AGENT_FRAMEWORK")
    ?? throw new InvalidOperationException(
        "Set AZURE_API_KEY_AGENT_FRAMEWORK environment variable");
```

However, `apiKey` is never passed to `AzureOpenAIClient`. The actual client uses Microsoft Entra ID through `DefaultAzureCredential`.

This has an important consequence: **the application refuses to start unless the API-key variable exists, even though it does not use that key.**

For the current Entra ID path, remove this variable requirement. If teaching API-key authentication, create a separate and explicit branch that passes an `AzureKeyCredential` to `AzureOpenAIClient`. Do not silently fall back from Entra ID to a key.

### 6.4 Configuring `DefaultAzureCredential`

The code configures a credential chain:

```csharp
var credentialOptions = new DefaultAzureCredentialOptions
{
    ExcludeEnvironmentCredential = false,
    ExcludeWorkloadIdentityCredential = true,
    ExcludeManagedIdentityCredential = true,
    ExcludeVisualStudioCredential = false,
    ExcludeVisualStudioCodeCredential = false,
    ExcludeAzureCliCredential = false,
    ExcludeAzurePowerShellCredential = false,
    ExcludeAzureDeveloperCliCredential = false,
    ExcludeInteractiveBrowserCredential = true,
    Diagnostics =
    {
        IsLoggingEnabled = true,
        IsAccountIdentifierLoggingEnabled = true
    }
};
```

`DefaultAzureCredential` checks a sequence of credential sources. This configuration is optimized for local development:

- Environment credentials are allowed.
- Visual Studio and command-line developer credentials are allowed.
- Interactive browser login is disabled.
- Workload identity and managed identity are disabled.

Disabling managed identity makes this configuration inappropriate for an Azure-hosted production application. In production, use a deterministic credential such as `ManagedIdentityCredential` and assign only the required Azure role.

`IsAccountIdentifierLoggingEnabled` can expose account identifiers in diagnostic logs. Enable it only when troubleshooting in a controlled development environment.

### 6.5 Testing the credential

The code requests a token:

```csharp
var credential = new DefaultAzureCredential(credentialOptions);

var token = await credential.GetTokenAsync(
    new Azure.Core.TokenRequestContext(
        new[] { "https://cognitiveservices.azure.com/.default" }),
    default);
```

This is a useful diagnostic because it separates token acquisition from the model request. A successful token proves that one credential in the chain authenticated. It does **not** prove:

- The identity has access to the target Azure OpenAI resource.
- The endpoint is correct.
- The deployment exists.
- The network allows access.
- The deployment has available quota.

Authorization is checked when the Azure OpenAI request is made.

### 6.6 Creating the chat client

The client is created with:

```csharp
var chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

There is a subtle bug in this section. The application creates and tests `credential`, but then constructs a **second** `DefaultAzureCredential` for the model client. The second instance does not use `credentialOptions`.

Therefore:

- The diagnostic credential and runtime credential may inspect different sources.
- The explicitly configured exclusions are ignored by the model client.
- Credential and token-cache reuse is lost.
- An unnecessary credential object is created.

The same instance should be reused:

```csharp
var chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        credential)
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

### 6.7 Converting the chat client into an agent

The agent is created with:

```csharp
var agent = chatClient.AsAIAgent(
    instructions: "You are good at telling jokes.",
    name: agentName);
```

`AsAIAgent` is an extension method. It wraps the chat client with Agent Framework behavior.

The two values have different purposes:

- `name` identifies the agent in logs, diagnostics, orchestration, or user interfaces.
- `instructions` guide the model's behavior for every turn.

Instructions are not a security boundary. They influence model behavior but cannot replace input validation, authorization, tool approval, data filtering, or output handling.

### 6.8 Creating a session

The code creates:

```csharp
AgentSession session = await agent.CreateSessionAsync();
```

The session is the key to multi-turn behavior. Reusing the same session allows a later prompt to depend on earlier prompts and responses.

Conceptually:

```text
Session
  Turn 1 user: Tell me a joke about a pirate.
  Turn 1 agent: [pirate joke]
  Turn 2 user: Add emojis to the joke.
  Turn 2 agent: [rewritten version of the earlier joke]
```

If turn 2 uses a new session, the phrase "the joke" has no session-specific referent.

The default chat-client agent uses local in-memory history. That means the conversation is available only while the process and session object remain alive. A new application run starts without the previous history unless the application explicitly serializes or moves conversation state to a persistent storage design.

### 6.9 Complete response execution

The current non-streaming call is:

```csharp
Console.WriteLine(
    await agent.RunAsync(
        "Tell me a joke about a pirate.",
        session));
```

`RunAsync` waits until the agent has produced the complete response. This is convenient for:

- Short responses.
- Background processing.
- Structured post-processing.
- Situations where partial output should not be displayed.

The intended second call is present but commented out:

```csharp
// Console.WriteLine(await agent.RunAsync(
//     "Now add some emojis to the joke and tell it in the voice of a pirate's parrot.",
//     session));
```

Uncommenting it demonstrates the simplest multi-turn exchange because both calls reuse `session`.

### 6.10 Streaming execution

The current code then calls:

```csharp
await foreach (var update in agent.RunStreamingAsync(
    "Tell me a joke about a pirate.",
    session))
{
    Console.WriteLine(update);
}
```

`RunStreamingAsync` returns updates as they arrive. Streaming improves perceived responsiveness because the user sees output before the complete answer has been generated.

Use `Console.Write(update)` rather than `Console.WriteLine(update)` when you want normal paragraph output:

```csharp
await foreach (var update in agent.RunStreamingAsync(
    prompt,
    session))
{
    Console.Write(update);
}

Console.WriteLine();
```

The current program reuses the same session for the initial complete call and both streaming calls. This is valid, but it makes the demonstration harder to reason about because the streaming section starts with existing history. A clearer lesson would use:

```csharp
AgentSession streamingSession = await agent.CreateSessionAsync();
```

for the streaming example.

### 6.11 Authentication error handling

The current catch block reports `AuthenticationFailedException` and rethrows it:

```csharp
catch (Azure.Identity.AuthenticationFailedException ex)
{
    Console.WriteLine($"Authentication failed: {ex.Message}");
    Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
    throw;
}
```

This is helpful during a lab because the process still exits as failed. In production:

- Send detailed exception information to protected structured logs.
- Show users a short safe message.
- Avoid exposing tokens, account identifiers, endpoints, or sensitive internal details.
- Handle service errors such as 403, 404, 429, and network failures separately.

---

## 7. What the program actually does today

The current execution sequence is:

1. Print the startup message.
2. Use a hard-coded deployment name and Azure OpenAI endpoint.
3. Require `AZURE_API_KEY_AGENT_FRAMEWORK`, even though the value is not used.
4. Create a locally oriented `DefaultAzureCredential`.
5. Request a Cognitive Services token as an authentication test.
6. Create a second, default-configured `DefaultAzureCredential`.
7. Create an Azure OpenAI chat client.
8. Adapt it to `IChatClient`.
9. Convert it to an Agent Framework agent.
10. Create one session.
11. Request one complete pirate joke.
12. Stream another pirate joke into the same session.
13. Stream a request to rewrite "the joke" using emojis and a parrot voice.
14. Catch identity authentication failure, print details, and rethrow.

Because all model calls share one session, the final prompt has access to every preceding turn in that session.

---

## 8. Code review findings

| Priority | Finding | Effect | Recommended change |
| --- | --- | --- | --- |
| High | The API-key environment variable is mandatory but unused. | Entra ID execution fails before authentication when the key variable is absent. | Remove the variable from the Entra path or implement an explicit key-auth mode. |
| High | The tested credential is not passed to `AzureOpenAIClient`. | Diagnostic options and exclusions do not govern the real request. | Reuse the `credential` instance. |
| High | The Azure OpenAI endpoint is hard-coded. | Source is tied to one resource and environment. | Read `AZURE_OPENAI_ENDPOINT` from configuration. |
| Medium | Managed identity and workload identity are excluded. | The credential configuration is unsuitable for an Azure-hosted app. | Use `ManagedIdentityCredential` in production. |
| Medium | Account identifier logging is enabled. | Diagnostic logs may contain identity information. | Enable only during controlled troubleshooting. |
| Medium | One session is shared by complete and streaming demonstrations. | The teaching output includes hidden prior context. | Use separate sessions for separate demonstrations. |
| Medium | The non-streaming follow-up is commented out. | The simplest multi-turn lesson is not shown at runtime. | Uncomment it or use an interactive loop. |
| Low | Streaming uses `Console.WriteLine(update)`. | Every update may appear on a separate line. | Use `Console.Write(update)` and one final newline. |
| Low | Three namespace imports are unused. | Adds noise for students. | Remove unused imports. |
| Low | No cancellation token is passed. | The user cannot gracefully cancel a long request. | Pass a `CancellationToken` through async calls. |
| Low | Packages are behind current available versions. | The sample differs from current package guidance. | Upgrade as a coordinated migration, not one package at a time. |

These findings do not prevent compilation. They affect clarity, configuration correctness, operational safety, and how accurately the sample teaches recommended patterns.

---

## 9. A cleaner teaching version using the current project architecture

The following version preserves the existing architecture while removing the major inconsistencies:

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace QuickStart_Agent_Framework_Multi_Turn_Convo;

internal class Program
{
    private static async Task Main()
    {
        string endpoint =
            Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? throw new InvalidOperationException(
                "Set AZURE_OPENAI_ENDPOINT.");

        string deploymentName =
            Environment.GetEnvironmentVariable(
                "AZURE_OPENAI_DEPLOYMENT_NAME")
            ?? "gpt-4.1-mini";

        var credential = new DefaultAzureCredential();

        IChatClient chatClient = new AzureOpenAIClient(
                new Uri(endpoint),
                credential)
            .GetChatClient(deploymentName)
            .AsIChatClient();

        AIAgent agent = chatClient.AsAIAgent(
            instructions: "You are good at telling jokes.",
            name: "AgentBond");

        AgentSession session = await agent.CreateSessionAsync();

        Console.WriteLine(await agent.RunAsync(
            "Tell me a joke about a pirate.",
            session));

        Console.WriteLine(await agent.RunAsync(
            "Now add emojis and retell the joke in the voice "
                + "of a pirate's parrot.",
            session));

        AgentSession streamingSession =
            await agent.CreateSessionAsync();

        await foreach (var update in agent.RunStreamingAsync(
            "Explain why pirates say arrr.",
            streamingSession))
        {
            Console.Write(update);
        }

        Console.WriteLine();
    }
}
```

This is still a development-oriented console example. For production, replace `DefaultAzureCredential` with a deterministic credential appropriate for the host, typically managed identity on Azure.

---

## 10. Azure prerequisites

You need:

1. An Azure subscription.
2. An Azure OpenAI resource or compatible Microsoft Foundry setup.
3. A deployed chat model.
4. The Azure OpenAI resource endpoint.
5. The model deployment name.
6. A local developer identity authorized to invoke the model.
7. The .NET 10 SDK.

For Microsoft Entra ID authentication, assign the invoking identity an appropriate data-plane role such as **Cognitive Services OpenAI User** at the narrowest practical scope.

For local development, sign in with one of the credential sources allowed by `DefaultAzureCredential`, for example:

```powershell
az login
az account show
```

Authentication answers, "Who is calling?" Azure role-based access control answers, "May this identity invoke this resource?"

---

## 11. Configure and run the project

### Recommended environment variables

Set:

| Variable | Example | Purpose |
| --- | --- | --- |
| `AZURE_OPENAI_ENDPOINT` | `https://my-resource.openai.azure.com/` | Azure OpenAI resource endpoint. |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | `gpt-4.1-mini` | Name assigned to the deployed model. |

The current uncorrected `Program.cs` also requires:

| Variable | Current behavior |
| --- | --- |
| `AZURE_API_KEY_AGENT_FRAMEWORK` | Must exist, but its value is not used. This is a code issue, not a true requirement of the Entra authentication path. |

For the current process only:

```powershell
$env:AZURE_OPENAI_ENDPOINT =
    "https://<resource-name>.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4.1-mini"
$env:AZURE_API_KEY_AGENT_FRAMEWORK = "unused-placeholder"
```

Do not use a real API key as the placeholder. The code does not use it.

### Restore

```powershell
dotnet restore
```

### Build

```powershell
dotnet build
```

### Run

```powershell
dotnet run
```

### Expected behavior

You should see:

1. A startup message.
2. A credential-test message.
3. Token acquisition confirmation.
4. A complete pirate joke.
5. Streaming updates for another pirate joke.
6. Streaming updates that rewrite the latest joke with emojis and a parrot voice.

Model wording is nondeterministic. Verify behavior and context retention rather than exact sentences.

---

## 12. Understanding multi-turn state

### Same session

```csharp
AgentSession session = await agent.CreateSessionAsync();

await agent.RunAsync("My favorite color is blue.", session);
await agent.RunAsync("What is my favorite color?", session);
```

The second turn can use the first turn's context.

### Different sessions

```csharp
AgentSession firstSession = await agent.CreateSessionAsync();
AgentSession secondSession = await agent.CreateSessionAsync();

await agent.RunAsync("My favorite color is blue.", firstSession);
await agent.RunAsync("What is my favorite color?", secondSession);
```

The second session should not know the information supplied only to the first session.

### Session lifetime

In this example, history is in memory:

- It is not durable across process restarts.
- It is not automatically shared across application instances.
- It should not be reused across unrelated users.
- Long histories increase token usage and may require compaction, summarization, or truncation.

In a real service, associate each user conversation with its own session and choose a persistence strategy that meets security, privacy, scale, and recovery requirements.

---

## 13. Complete responses versus streaming

| Behavior | `RunAsync` | `RunStreamingAsync` |
| --- | --- | --- |
| Returns | Completed agent response | Asynchronous sequence of updates |
| First visible output | After generation completes | As updates arrive |
| Simplicity | Simpler | Requires `await foreach` |
| Best for | Short answers, processing, structured results | Chat UI, long answers, responsive consoles |
| Conversation state | Uses the supplied session | Uses the supplied session |

Streaming changes delivery, not memory. Multi-turn behavior comes from reusing `AgentSession`.

---

## 14. Authentication guidance

### Local development

`DefaultAzureCredential` is convenient because it can use supported developer sign-ins such as Visual Studio, Azure CLI, Azure PowerShell, or Azure Developer CLI.

### Production

Prefer a deterministic credential:

```csharp
var credential = new ManagedIdentityCredential();
```

or the user-assigned managed identity form when required.

Benefits include:

- No application secret to rotate.
- A predictable production identity.
- Azure RBAC controls access.
- Fewer unexpected credential-chain fallbacks.
- Better alignment with least privilege.

Reuse credential and Azure client instances. Credential reuse allows token caching and reduces unnecessary requests to Microsoft Entra ID.

### API keys

Use an API key only when identity-based authentication cannot be used and organizational policy permits it.

If a key is necessary:

- Store it in Azure Key Vault or another approved secret store.
- Never commit it.
- Never print it.
- Never use a fake fallback that hides identity configuration failures.
- Rotate it immediately if exposed.

---

## 15. Troubleshooting

### The application immediately asks for `AZURE_API_KEY_AGENT_FRAMEWORK`

Cause: the current source requires an unused environment variable.

Best fix: remove that variable read from the Entra ID path.

Temporary lab workaround:

```powershell
$env:AZURE_API_KEY_AGENT_FRAMEWORK = "unused-placeholder"
```

### `CredentialUnavailableException`

No enabled credential source could authenticate.

Check:

```powershell
az login
az account show
```

Also verify that the credential source is not excluded in `DefaultAzureCredentialOptions`.

### `AuthenticationFailedException`

A credential source was attempted but could not authenticate. Inspect the identity diagnostics in a protected development environment.

### HTTP 401

Possible causes:

- Invalid or expired credential.
- Endpoint and credential belong to different resources or tenants.
- An API key is invalid when using key authentication.

### HTTP 403

Possible causes:

- The identity lacks the required Azure role.
- The role was assigned at the wrong scope.
- A new role assignment has not propagated.
- A firewall, private endpoint, or public-network policy blocks access.

### HTTP 404

Possible causes:

- The deployment name is wrong.
- The deployment exists in a different resource.
- A Foundry project endpoint was supplied to `AzureOpenAIClient`.

### HTTP 429

The deployment is rate-limited or lacks available quota. Respect the service retry interval and review deployment capacity.

### The second turn forgets the first

Check that both calls receive the same `AgentSession` instance.

### Streaming output has too many line breaks

Replace:

```csharp
Console.WriteLine(update);
```

with:

```csharp
Console.Write(update);
```

and write one newline after the loop.

---

## 16. Student exercises

1. Remove the unused namespaces and rebuild.
2. Move the endpoint and deployment name to environment variables.
3. Remove the unused API-key requirement.
4. Pass the tested `credential` instance into `AzureOpenAIClient`.
5. Uncomment the non-streaming follow-up and confirm that it refers to the first joke.
6. Create a second session and prove that it does not know the first session's joke.
7. Change streaming output from `WriteLine` to `Write`.
8. Build an interactive loop that exits when the user enters `exit`.
9. Add `CancellationToken` support so Ctrl+C can cancel a request.
10. Replace the joke instructions with a technical tutor persona.
11. Inspect in-memory history through the agent's history provider.
12. Research conversation compaction before creating a long-running chat.
13. Plan a coordinated upgrade from Agent Framework `1.3.0` to the current package family.

---

## 17. Knowledge check

1. Why does `AzureOpenAIClient` require the Azure OpenAI resource endpoint rather than the Foundry project endpoint?
2. What does `GetChatClient(deploymentName)` select?
3. What abstraction is introduced by `AsIChatClient()`?
4. What behavior is introduced by `AsAIAgent()`?
5. Why does reusing `AgentSession` enable a prompt such as "rewrite the joke"?
6. Does streaming create conversation memory?
7. Why is the current API-key variable misleading?
8. Why should the tested credential instance be reused by the model client?
9. Why is `DefaultAzureCredential` more appropriate for development than for production?
10. Why should related top-level AI packages be upgraded together?
11. What happens to the in-memory conversation after the process exits?
12. Why should separate users never share one `AgentSession`?

---

## 18. Recommended next architecture steps

After mastering this example, expand it in this order:

1. **Configuration:** use environment variables or .NET configuration providers.
2. **Interactive chat:** collect prompts in a loop while reusing one session.
3. **Cancellation:** support request cancellation and graceful shutdown.
4. **Dependency injection:** register credentials, clients, and agents once.
5. **Observability:** add structured logging, tracing, token usage, and latency.
6. **Tools:** add approved functions that let the agent perform controlled actions.
7. **Persistent conversations:** choose secure durable storage when sessions must survive restarts.
8. **History management:** add compaction or summarization for long conversations.
9. **Evaluation:** create repeatable tests for instruction following, quality, safety, and regressions.
10. **Production identity:** use managed identity and least-privilege Azure RBAC.

---

## 19. Official references

- Microsoft Agent Framework with Azure OpenAI:  
  <https://learn.microsoft.com/agent-framework/integrations/by-component/model-providers/azure-openai>
- Agent Framework multi-turn conversations:  
  <https://learn.microsoft.com/agent-framework/get-started/multi-turn?pivots=programming-language-csharp>
- Agent Framework conversation storage:  
  <https://learn.microsoft.com/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp>
- Azure Identity authentication best practices for .NET:  
  <https://learn.microsoft.com/dotnet/azure/sdk/authentication/best-practices>
- Azure OpenAI .NET client library:  
  <https://learn.microsoft.com/dotnet/api/overview/azure/ai.openai-readme>
- Keyless authentication for Azure AI services:  
  <https://learn.microsoft.com/azure/developer/ai/keyless-connections>

Microsoft Agent Framework evolves quickly. Treat package versions, provider packages, and recommended client APIs as version-sensitive, and verify current official guidance before starting a new production implementation.
