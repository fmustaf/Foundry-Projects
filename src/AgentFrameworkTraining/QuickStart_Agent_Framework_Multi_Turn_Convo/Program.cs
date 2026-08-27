using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel.Primitives;

namespace QuickStart_Agent_Framework_Multi_Turn_Convo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Getting Started with the Agent Framework ... ");

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

                // ✅ Step 1: create chat client
                var chatClient = new AzureOpenAIClient(
                        new Uri(endpoint),
                        new DefaultAzureCredential())
                    .GetChatClient(deploymentName)
                    .AsIChatClient();   // ⚠️ THIS LINE IS REQUIRED

                // ✅ Step 2: convert to agent
                var agent = chatClient.AsAIAgent(
                    instructions: "You are good at telling jokes.",
                    name: agentName
                );
                
                AgentSession session = await agent.CreateSessionAsync();

                // Invoke the agent and output the text result.
                Console.WriteLine(await agent.RunAsync("Tell me a joke about a pirate.", session));
                //Console.WriteLine(await agent.RunAsync("Now add some emojis to the joke and tell it in the voice of a pirate's parrot.", session));

                // Invoke the agent with streaming support.
                await foreach (var update in agent.RunStreamingAsync("Tell me a joke about a pirate.", session))
                {
                    Console.WriteLine(update);
                }
                await foreach (var update in agent.RunStreamingAsync("Now add some emojis to the joke and tell it in the voice of a pirate's parrot.", session))
                {
                    Console.WriteLine(update);
                }
            }
            catch (Azure.Identity.AuthenticationFailedException ex)
            {
                Console.WriteLine($"Authentication failed: {ex.Message}");
                Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                throw;
            }
        }
    }
}
