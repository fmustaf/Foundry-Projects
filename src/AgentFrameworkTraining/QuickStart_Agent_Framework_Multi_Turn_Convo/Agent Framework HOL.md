# Microsoft Agent Framework Hands-on Lab

## Build a multi-turn Azure OpenAI agent with .NET

**Estimated time:** 60-75 minutes  
**Language:** C#  
**Framework:** .NET 10  
**Model:** `gpt-4.1-mini`

## Why this training matters

Generative AI becomes valuable when it moves beyond a chat window and becomes part of an application. Organizations need developers who can connect models to real software, give agents purposeful instructions, preserve conversation context, stream responsive results, and authenticate securely to enterprise resources. The Microsoft Agent Framework SDK provides the programming model for turning those requirements into maintainable .NET solutions.

Learning the SDK helps you move from experimenting with prompts to engineering agent-powered applications. Instead of making isolated model calls, you will learn how the client, agent, and session layers work together. These are foundational skills you can apply to assistants, workflow automation, customer experiences, developer tools, and other applications that need multi-turn AI behavior.

This lab uses a small pirate-joke scenario so you can focus on the architecture without being distracted by business complexity. The scenario is simple, but the implementation teaches the same building blocks required by larger solutions:

- Connect application code to a model deployed in Microsoft Foundry.
- Use Azure identity and role-based access control rather than embedding credentials in source code.
- Define an agent's purpose and behavior through instructions.
- Maintain context across related requests with an agent session.
- Choose between complete responses and streaming output for the user experience.
- Keep the agent code independent of the authentication method.

### What is in it for you

By engaging with the exercises and writing each section of code yourself, you will understand not only *what* to type, but *why* each SDK layer exists. You will leave with a working reference implementation, a repeatable setup process, and a mental model you can use to diagnose authentication, endpoint, deployment, and conversation-state problems.

Do not treat the lab as a copy-and-paste exercise. Run the application at each checkpoint, inspect how the behavior changes, and complete the knowledge checks and optional challenges. The more you experiment with instructions, sessions, and streaming, the better prepared you will be to adapt the SDK to your own use cases after the training.

## Lab objectives

By the end of this lab, you will be able to:

1. Create a Microsoft Foundry project and deploy a `gpt-4.1-mini` model.
2. Configure a .NET console application with the Agent Framework packages used by the starter solution.
3. Authenticate to Azure with `DefaultAzureCredential`.
4. Adapt an Azure OpenAI chat client to an Agent Framework agent.
5. Preserve conversation context with an `AgentSession`.
6. Run an agent with complete and streaming responses.
7. Use an API key as an explicit fallback when Microsoft Entra ID authentication cannot be configured.

## What you will build

The completed application creates an agent named `AgentBond`. The agent tells a pirate joke, remembers that joke during a second turn, and rewrites it with emojis in the voice of a pirate's parrot.

The application flow is:

```text
AzureOpenAIClient
    -> ChatClient
    -> IChatClient adapter
    -> AIAgent
    -> AgentSession
    -> RunAsync / RunStreamingAsync
```

## Prerequisites

- An Azure subscription in which you can create resources and model deployments.
- Permission to create a Microsoft Foundry project.
- .NET 8 OR .NET 10 SDK.
- Visual Studio 2022 or 2026
- Azure CLI or another supported local developer credential.

> [!IMPORTANT]
> Azure portal labels can change. If a label in this lab differs slightly from the portal, use the equivalent **Microsoft Foundry**, **Models + endpoints**, **Deployments**, or **Keys and Endpoint** page.

---

# Exercise 1: Create the Azure resources

## Step 1: Create a Microsoft Foundry project

1. Sign in to the [Azure portal](https://portal.azure.com).
2. Search for **Microsoft Foundry**.
3. Create or open a Microsoft Foundry resource.
4. Open the resource in the [Microsoft Foundry portal](https://ai.azure.com).
5. Select **Create project**.
6. Enter a unique project name.
7. Select the Azure subscription, resource group, and supported region.
8. Create the project and wait for provisioning to finish.

## Step 2: Deploy `gpt-4.1-mini`

1. In the Foundry portal, open the project.
2. Open **Models + endpoints** or the model catalog.
3. Select **Deploy model**.
4. Find and select **gpt-4.1-mini**.
5. Choose a deployment type and quota supported by the lab subscription.
6. Set the deployment name to:

   ```text
   gpt-4.1-mini
   ```

7. Create the deployment and wait until its status is ready.

> [!NOTE]
> The deployment name is supplied to `GetChatClient`. A deployment name is not necessarily the same as the model's catalog name. This lab deliberately uses `gpt-4.1-mini` for both.

Your identity must be authorized to invoke the model. An administrator can assign the **Cognitive Services OpenAI User** role on the Azure OpenAI resource:

1. Open the Azure OpenAI resource in the Azure portal.
2. Select **Access control (IAM)**.
3. Select **Add role assignment**.
4. Select **Cognitive Services OpenAI User**.
5. Assign the role to the student identity.

Role assignments can take several minutes to propagate.

## Step 3: Record the endpoints and API key securely

On the project overview page, record the **project endpoint** for future Foundry project exercises. It commonly resembles:

```text
https://<resource-name>.services.ai.azure.com/api/projects/<project-name>
```

For this lab, also open the deployed model or its parent Azure OpenAI resource and record the **Azure OpenAI resource endpoint**. It commonly resembles:

```text
https://<resource-name>.openai.azure.com/
```

The code in this lab creates an `AzureOpenAIClient`, so it uses the **Azure OpenAI resource endpoint**, not the Foundry project endpoint.

Open **Keys and Endpoint** and copy one API key only if Entra ID auth fails then this is a fallback for authentication.

> [!CAUTION]
> Do not paste keys into `Program.cs`, source control, chat, screenshots, or lab notes. Store the key in an approved password manager, Azure Key Vault, or another encrypted file location outside the repository. The application reads secrets from environment variables.

## Step 4: Configure environment variables

Add the configuration as Windows user environment variables so it is available to Visual Studio and new terminal sessions:

1. Press the **Windows key** and search for **Edit environment variables for your account**.
2. Open the matching System Settings result.
3. In the **User variables** section, select **New**.
4. Create the following variable:

   | Field | Value |
   | --- | --- |
   | Variable name | `AZURE_OPENAI_ENDPOINT` |
   | Variable value | `https://<resource-name>.openai.azure.com/` |

5. Select **New** again and create the following variable:

   | Field | Value |
   | --- | --- |
   | Variable name | `AZURE_OPENAI_DEPLOYMENT_NAME` |
   | Variable value | `gpt-4.1-mini` |

6. Select **OK** to close each dialog and save the variables.
7. Close and reopen Visual Studio and any terminal windows so the applications load the new values.

Use **User variables**, not **System variables**. User variables do not require administrator access and are available only to your Windows account.

Do not create the API key variable yet. Microsoft Entra ID is the primary authentication method in this lab. If you reach the fallback exercise, return to **Edit environment variables for your account** and create:

| Field | Value |
| --- | --- |
| Variable name | `AZURE_API_KEY_AGENT_FRAMEWORK` |
| Variable value | The API key retrieved from its secure location |

> [!CAUTION]
> Windows environment variables are persistent but are not an encrypted secret store. Create the API key variable only when the fallback exercise requires it, do not use a system-wide variable, and remove it after the lab.

> [!NOTE]
> You need to restart Visual Studio after adding the environment variables, in case you already have Visual Studio Open.

---

# Exercise 2: Prepare the .NET project

## Step 1: Create the starter solution in Visual Studio

Sign in to your Azure subscription via Azure CLI. Follow the prompts to complete the login to your Azure subscription.

```text
az login --use-device-code
```
If you have access to more than one subscription, once logged in verify you are using the right subscription:
```text
az account show
```
If you are not in the intended subscription:
```text
az account set --subscription yoursubscriptionid
```

> [!NOTE]
> We are doing this upfront so we can use the DefaultAzureCredential method to authenticate instead of using the API Key. 
using API Key is not a best practice. If the DefaultAzureCredential fails for you, then revert to using the API Key so you may proceed with the lab.

Sign in in Visual Studio to your Azure Tenant. Click Top Right hand corner for your profile in Visual Studio, Add an account, Work or School Account, use your Azure Subscription Credentials to add the account.

Now, create a Console Application in Visual Studio, set the Project Name to 'QuickStart_Agent_Framework_Multi_Turn_Convo'. Visual Studio will create the following solution:

```text
QuickStart_Agent_Framework_Multi_Turn_Convo.sln
```

The project targets .NET 10 and has nullable reference types enabled. 

> [!NOTE]
> If you do not have .NET 10 installed, you may target .NET 8, the code will work with .NET 8.

## Step 2: Install the NuGet packages

From the directory containing
`QuickStart_Agent_Framework_Multi_Turn_Convo.csproj`, run:

```powershell
dotnet add package Azure.AI.OpenAI --version 2.1.0
dotnet add package Azure.Identity --version 1.21.0
dotnet add package Microsoft.Agents.AI --version 1.3.0
dotnet add package Microsoft.Extensions.AI.OpenAI --version 10.5.0
```

These are the exact package references used by the starter solution:

| Package | Purpose |
| --- | --- |
| `Azure.AI.OpenAI` | Creates the Azure OpenAI client and accesses the deployed chat model. |
| `Azure.Identity` | Supplies `DefaultAzureCredential` for Microsoft Entra ID authentication. |
| `Microsoft.Agents.AI` | Supplies the Agent Framework abstractions, agent, session, and run methods. |
| `Microsoft.Extensions.AI.OpenAI` | Adapts the OpenAI chat client to the common `IChatClient` abstraction. |

Restore and build the project:

```powershell
dotnet restore
dotnet build
```

---

# Exercise 3: Create the Azure OpenAI client

Replace the contents of `Program.cs` as directed in each step.

## Step 1: Add the namespaces and application shell

Start with:

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace QuickStart_Agent_Framework_Multi_Turn_Convo;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Getting Started with the Agent Framework...");
    }
}
```

The important namespaces are:

- `Azure.AI.OpenAI` for `AzureOpenAIClient`.
- `Azure.Identity` for `DefaultAzureCredential`.
- `Microsoft.Agents.AI` for agent and session APIs.
- `Microsoft.Extensions.AI` for `IChatClient` and its adapter.

## Step 2: Read configuration from environment variables

Inside `Main`, after `Console.WriteLine`, add:

```csharp
string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException(
        "Set the AZURE_OPENAI_ENDPOINT environment variable.");

string deploymentName =
    Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? "gpt-4.1-mini";

const string agentName = "AgentBond";
```

This keeps environment-specific configuration out of source code.

## Step 3: Create the Azure credential

Add:

```csharp
var credential = new DefaultAzureCredential();
```

`DefaultAzureCredential` is an Azure Identity credential chain. During local development, it can use a supported developer sign-in such as Azure CLI or Visual Studio. In an Azure-hosted production application, prefer a managed identity and grant it only the permissions it needs.

> [!IMPORTANT]
> The credential proves who the caller is. Azure role-based access control determines whether that identity can invoke the model.

## Step 4: Create and adapt the chat client

Add:

```csharp
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        credential)
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

Read the chain from top to bottom:

1. `new AzureOpenAIClient(...)` connects to the Azure OpenAI resource.
2. `GetChatClient(deploymentName)` selects the deployed chat model.
3. `AsIChatClient()` adapts the provider-specific chat client to the common `IChatClient` abstraction used by Agent Framework.

`AsIChatClient()` is required in this version of the starter project.

---

# Exercise 4: Construct and invoke the agent

## Step 1: Create the agent

Add:

```csharp
var agent = chatClient.AsAIAgent(
    instructions: "You are good at telling jokes.",
    name: agentName);
```

This is the constructor step that creates and instantiates the agent object.

Strictly speaking, `AsAIAgent` is an extension method rather than a C# constructor. It constructs the Agent Framework agent from the `IChatClient` and configures:

- `instructions`: the agent's behavior and role.
- `name`: a readable identity for the agent.

## Step 2: Create a conversation session

Add:

```csharp
AgentSession session = await agent.CreateSessionAsync();
```

An `AgentSession` preserves the conversation context supplied to the agent. Reuse the same session for related turns. A different session starts a separate conversation.

## Step 3: Run the first turn

Add:

```csharp
Console.WriteLine(
    await agent.RunAsync(
        "Tell me a joke about a pirate.",
        session));
```

`RunAsync` waits for the completed agent response.

## Step 4: Run a context-dependent second turn

Add:

```csharp
Console.WriteLine(
    await agent.RunAsync(
        "Now add some emojis to the joke and tell it in the voice of a pirate's parrot.",
        session));
```

The second prompt says "the joke" rather than repeating the first prompt. It works because both calls share the same `AgentSession`.

## Step 5: Run the application

```powershell
dotnet run
```

Check that:

1. The first response contains a pirate joke.
2. The second response transforms the earlier joke.
3. No endpoint, token, or API key is written to the console.

---

# Exercise 5: Stream agent responses

`RunStreamingAsync` returns updates as they arrive instead of waiting for the entire response.

## Step 1: Create a separate streaming session

Add:

```csharp
AgentSession streamingSession = await agent.CreateSessionAsync();
```

Using a separate session keeps this demonstration independent of the completed-response conversation.

## Step 2: Stream the first turn

Add:

```csharp
await foreach (var update in agent.RunStreamingAsync(
    "Tell me a joke about a pirate.",
    streamingSession))
{
    Console.Write(update);
}

Console.WriteLine();
```

## Step 3: Stream the follow-up turn

Add:

```csharp
await foreach (var update in agent.RunStreamingAsync(
    "Now add some emojis to the joke and tell it in the voice of a pirate's parrot.",
    streamingSession))
{
    Console.Write(update);
}

Console.WriteLine();
```

## Step 4: Run the application again

```powershell
dotnet run
```

Observe that text is displayed incrementally. The same multi-turn behavior is retained because both streaming calls use `streamingSession`.

---

# Exercise 6 (Optional): Use an API key only when Entra ID authentication is blocked

Microsoft Entra ID with Azure role-based access control is the preferred approach.

## Step 1: Load the key from the environment

Replace:

```csharp
var credential = new DefaultAzureCredential();
```

with:

```csharp
string apiKey = Environment.GetEnvironmentVariable("AZURE_API_KEY_AGENT_FRAMEWORK")
    ?? throw new InvalidOperationException(
        "Set the AZURE_API_KEY_AGENT_FRAMEWORK environment variable.");
```

## Step 2: Replace the Azure OpenAI client credential

Replace the `IChatClient` construction with:

```csharp
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

The rest of the Agent Framework code does not change. The agent, session, non-streaming calls, and streaming calls are independent of the selected Azure OpenAI authentication method.

## Step 3: Set the key and run

Retrieve the API key from the secure location selected in Exercise 1:

```powershell
$env:AZURE_API_KEY_AGENT_FRAMEWORK = "<your-key>"
dotnet run
```

After the lab, clear the process-scoped value:

```powershell
Remove-Item Env:AZURE_API_KEY_AGENT_FRAMEWORK
```

If a key was exposed in source control, terminal history, chat, or a screenshot, rotate it immediately in Azure.

---

# Completed `Program.cs` using Microsoft Entra ID

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace QuickStart_Agent_Framework_Multi_Turn_Convo;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Getting Started with the Agent Framework...");

        string endpoint =
            Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? throw new InvalidOperationException(
                "Set the AZURE_OPENAI_ENDPOINT environment variable.");

        string deploymentName =
            Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
            ?? "gpt-4.1-mini";

        const string agentName = "AgentBond";

        var credential = new DefaultAzureCredential();

        IChatClient chatClient = new AzureOpenAIClient(
                new Uri(endpoint),
                credential)
            .GetChatClient(deploymentName)
            .AsIChatClient();

        var agent = chatClient.AsAIAgent(
            instructions: "You are good at telling jokes.",
            name: agentName);

        AgentSession session = await agent.CreateSessionAsync();

        Console.WriteLine(
            await agent.RunAsync(
                "Tell me a joke about a pirate.",
                session));

        Console.WriteLine(
            await agent.RunAsync(
                "Now add some emojis to the joke and tell it in the voice of a pirate's parrot.",
                session));

        AgentSession streamingSession = await agent.CreateSessionAsync();

        await foreach (var update in agent.RunStreamingAsync(
            "Tell me a joke about a pirate.",
            streamingSession))
        {
            Console.Write(update);
        }

        Console.WriteLine();

        await foreach (var update in agent.RunStreamingAsync(
            "Now add some emojis to the joke and tell it in the voice of a pirate's parrot.",
            streamingSession))
        {
            Console.Write(update);
        }

        Console.WriteLine();
    }
}
```

---

# Troubleshooting

## `CredentialUnavailableException` or `AuthenticationFailedException`

1. Run `az login`.
2. Run `az account show` and verify the subscription.
3. Confirm the signed-in identity has **Cognitive Services OpenAI User** on the correct resource.
4. Wait for a new role assignment to propagate.
5. Confirm the Azure OpenAI resource allows the network path used by the lab computer.
6. If configuring identity access is unsuccessful, complete the explicit API key fallback exercise.

## HTTP 401: Unauthorized

- The credential may be valid but belong to an identity without the required role.
- The API key may belong to a different resource.
- The API key may have been regenerated.
- The endpoint and credential must belong to the same Azure OpenAI resource.

## HTTP 403: Forbidden

- Check role assignments and scope.
- Check resource firewall, private endpoint, and public network access settings.
- Check organizational policy restrictions.

## HTTP 404: Deployment not found

- `AZURE_OPENAI_DEPLOYMENT_NAME` must contain the deployment name, not merely the model family name.
- Confirm that the deployment exists in the resource identified by `AZURE_OPENAI_ENDPOINT`.
- Do not pass the Foundry project endpoint to `AzureOpenAIClient`.

## HTTP 429: Rate limit or quota exceeded

- Wait and retry after the service-provided delay.
- Confirm the deployment has available quota.
- Coordinate with other lab participants who share the deployment.

## The second response does not remember the first

- Pass the same `AgentSession` instance to both related calls.
- Do not create a new session between turns.

## Streaming output appears one item per line

Use `Console.Write(update)` inside the streaming loop and one `Console.WriteLine()` after the loop. `Console.WriteLine(update)` adds a newline for every update.

---

# Knowledge check

1. What is the difference between the Foundry project endpoint and the Azure OpenAI resource endpoint?
2. Why is `AsIChatClient()` used before `AsAIAgent()`?
3. Which line constructs the agent?
4. What information is retained by reusing an `AgentSession`?
5. How does `RunStreamingAsync` differ from `RunAsync`?
6. Why should Microsoft Entra ID be preferred over an API key?
7. Why should an application not silently fall back from identity authentication to API key authentication?

# Optional challenges

1. Change the agent instructions to create a technical teaching assistant.
2. Add a third turn that asks the agent to explain why its rewritten joke still relates to the original.
3. Create a second session and compare its lack of prior conversation context.
4. Move the prompts into command-line input while preserving the same session.
5. Add cancellation-token support to the asynchronous calls.

# Clean up

To avoid unexpected Azure charges:

1. Delete the model deployment if it was created only for this lab.
2. Delete the Foundry project or resource group if instructed.
3. Clear `AZURE_API_KEY_AGENT_FRAMEWORK` from the terminal.
4. Remove any temporary local copies of the API key.

# References

- [Create a Microsoft Foundry project](https://learn.microsoft.com/azure/foundry/how-to/create-projects)
- [Microsoft Foundry documentation](https://learn.microsoft.com/azure/foundry/)
- [Microsoft Agent Framework with Azure OpenAI](https://learn.microsoft.com/agent-framework/integrations/by-component/model-providers/azure-openai)
- [Azure OpenAI client library for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/ai.openai-readme)
- [DefaultAzureCredential](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential)
- [Keyless authentication for Azure AI services](https://learn.microsoft.com/azure/developer/ai/keyless-connections)
