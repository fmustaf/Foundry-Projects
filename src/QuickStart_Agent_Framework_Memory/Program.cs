//using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Chat;
using QuickStart_Agent_Framework_Memory;
using System.ClientModel.Primitives;
using System.Text;
using System.Text;
using System.Text.Json;

namespace QuickStart_Agent_Framework_Memory
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Getting Started with the Agent Framework ... ");

            //        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            //?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT");
            //        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";

            const string deploymentName = "gpt-4.1-mini";
            // Use this endpoint when you are calling the model deployed in Azure OpenAI.
            //const string endpoint = "https://demofaisalnewui-1423-resource.openai.azure.com/openai/v1/";
            const string endpoint = "https://demofaisalnewui-1423-resource.openai.azure.com/";
            // Use this endpoint when you are calling the agent project deployed in Azure OpenAI. This is the endpoint for the project, not the model deployment.
            //const string endpoint = "https://demofaisalnewui-1423-resource.services.ai.azure.com/api/projects/demofaisalnewui-1423";

            const string agentName = "AgentBond";

            // Get API key from environment variable
            var apiKey = Environment.GetEnvironmentVariable("AZURE_API_KEY_AGENT_FRAMEWORK")
                ?? throw new InvalidOperationException("Set AZURE_API_KEY_AGENT_FRAMEWORK environment variable");

            // Add diagnostic options to see which credential is being used
            var credentialOptions = new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeWorkloadIdentityCredential = true,
                ExcludeManagedIdentityCredential = true,
                ExcludeSharedTokenCacheCredential = false,
                ExcludeVisualStudioCredential = false,
                ExcludeVisualStudioCodeCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeAzurePowerShellCredential = false,
                ExcludeAzureDeveloperCliCredential = false,
                ExcludeInteractiveBrowserCredential = true,
                Diagnostics = { IsLoggingEnabled = true, IsAccountIdentifierLoggingEnabled = true }
            };

            try
            {
                var credential = new DefaultAzureCredential(credentialOptions);

                // Test the credential before using it
                Console.WriteLine("Testing credential...");
                var token = await credential.GetTokenAsync(
                    new Azure.Core.TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
                    default);
                Console.WriteLine($"Token acquired successfully. Expires: {token.ExpiresOn}");

                // WARNING: DefaultAzureCredential is convenient for development but requires careful consideration in production.
                // In production, consider using a specific credential (e.g., ManagedIdentityCredential) to avoid
                // latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

                // Even though the token is acquired successfully, the authentication might still fail if the credential is not properly configured for the Azure OpenAI resource.
                // Ensure that the identity used by DefaultAzureCredential has the necessary permissions to access the Azure OpenAI resource. This is causing auth 401 issue.
                // Interim using the API Key, will come back to troubleshoot the DefaultAzureCredential issue later.               

                // ✅ Step 1: create chat client
                var chatClient = new AzureOpenAIClient(
                        new Uri(endpoint),
                        new DefaultAzureCredential())
                    .GetChatClient(deploymentName)
                    .AsIChatClient();   // ⚠️ THIS LINE IS REQUIRED

                // ✅ Step 2: convert to agent
                var agent = chatClient.AsAIAgent(
                    instructions: "You are a friendly assistant. Always address the user by their name.",
                    name: agentName
                );

                AgentSession session = await agent.CreateSessionAsync();

                Console.WriteLine(">> Use session with blank memory\n");

                // Invoke the agent and output the text result.
                Console.WriteLine(await agent.RunAsync("Hello, what is the square root of 9?", session));
                Console.WriteLine(await agent.RunAsync("My name is Pink Panther", session));
                Console.WriteLine(await agent.RunAsync("I am 20 years old", session));

                // We can serialize the session. The serialized state will include the state of the memory component.
                JsonElement sessionElement = await agent.SerializeSessionAsync(session);

                Console.WriteLine("\n>> Use deserialized session with previously created memories\n");

                // Later we can deserialize the session and continue the conversation with the previous memory component state.
                var deserializedSession = await agent.DeserializeSessionAsync(sessionElement);
                Console.WriteLine(await agent.RunAsync("What is my name and age?", deserializedSession));

                Console.WriteLine("\n>> Read memories using memory component\n");

                // It's possible to access the memory component via the agent's GetService method.
                var userInfo = agent.GetService<UserInfoMemory>()?.GetUserInfo(deserializedSession);

                // Output the user info that was captured by the memory component.
                Console.WriteLine($"MEMORY - User Name: {userInfo?.UserName}");
                Console.WriteLine($"MEMORY - User Age: {userInfo?.UserAge}");

                Console.WriteLine("\n>> Use new session with previously created memories\n");

                // It is also possible to set the memories using a memory component on an individual session.
                // This is useful if we want to start a new session, but have it share the same memories as a previous session.
                var newSession = await agent.CreateSessionAsync();
                if (userInfo is not null && agent.GetService<UserInfoMemory>() is UserInfoMemory newSessionMemory)
                {
                    newSessionMemory.SetUserInfo(newSession, userInfo);
                }

                // Invoke the agent and output the text result.
                // This time the agent should remember the user's name and use it in the response.
                Console.WriteLine(await agent.RunAsync("What is my name and age?", newSession));

            }
            catch (Azure.Identity.AuthenticationFailedException ex)
            {
                Console.WriteLine($"Authentication failed: {ex.Message}");
                Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                throw;
            }
        }
    }


        /// <summary>
        /// Sample memory component that can remember a user's name and age.
        /// </summary>
        internal sealed class UserInfoMemory : AIContextProvider
        {
            private readonly ProviderSessionState<UserInfo> _sessionState;
            private IReadOnlyList<string>? _stateKeys;
            private readonly IChatClient _chatClient;

            public UserInfoMemory(IChatClient chatClient, Func<AgentSession?, UserInfo>? stateInitializer = null)
            {
                this._sessionState = new ProviderSessionState<UserInfo>(
                    stateInitializer ?? (_ => new UserInfo()),
                    this.GetType().Name);
                this._chatClient = chatClient;
            }

            public override IReadOnlyList<string> StateKeys => this._stateKeys ??= [this._sessionState.StateKey];

            public UserInfo GetUserInfo(AgentSession session)
                => this._sessionState.GetOrInitializeState(session);

            public void SetUserInfo(AgentSession session, UserInfo userInfo)
                => this._sessionState.SaveState(session, userInfo);

            protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
            {
                var userInfo = this._sessionState.GetOrInitializeState(context.Session);

                // Try and extract the user name and age from the message if we don't have it already and it's a user message.
                if ((userInfo.UserName is null || userInfo.UserAge is null) && context.RequestMessages.Any(x => x.Role == ChatRole.User))
                {
                    var result = await this._chatClient.GetResponseAsync<UserInfo>(
                        context.RequestMessages,
                        new ChatOptions()
                        {
                            Instructions = "Extract the user's name and age from the message if present. If not present return nulls."
                        },
                        cancellationToken: cancellationToken);

                    userInfo.UserName ??= result.Result.UserName;
                    userInfo.UserAge ??= result.Result.UserAge;
                }

                this._sessionState.SaveState(context.Session, userInfo);
            }

            protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
            {
                var userInfo = this._sessionState.GetOrInitializeState(context.Session);

                StringBuilder instructions = new();

                // If we don't already know the user's name and age, add instructions to ask for them, otherwise just provide what we have to the context.
                instructions
                    .AppendLine(
                        userInfo.UserName is null ?
                            "Ask the user for their name and politely decline to answer any questions until they provide it." :
                            $"The user's name is {userInfo.UserName}.")
                    .AppendLine(
                        userInfo.UserAge is null ?
                            "Ask the user for their age and politely decline to answer any questions until they provide it." :
                            $"The user's age is {userInfo.UserAge}.");

                return new ValueTask<AIContext>(new AIContext
                {
                    Instructions = instructions.ToString()
                });
            }
        }

        internal sealed class UserInfo
        {
            public string? UserName { get; set; }
            public int? UserAge { get; set; }
        }
    
}
