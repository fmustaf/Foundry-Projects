using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.AI;

namespace QuickStart_Agent_Framework_host_agent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            //?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT");
            //        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";

            const string deploymentName = "gpt-4.1-mini";
            // Use this endpoint when you are calling the model deployed in Azure OpenAI.
            //const string endpoint = "https://demofaisalnewui-1423-resource.openai.azure.com/openai/v1/";
            const string endpoint = "https://demofaisalnewui-1423-resource.openai.azure.com/";
            // Use this endpoint when you are calling the agent project deployed in Azure OpenAI. This is the endpoint for the project, not the model deployment.
            //const string endpoint = "https://demofaisalnewui-1423-resource.services.ai.azure.com/api/projects/demofaisalnewui-1423";         

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

                // Set up an AI agent following the standard Microsoft Agent Framework pattern.
                // WARNING: DefaultAzureCredential is convenient for development but requires careful consideration in production.
                // In production, consider using a specific credential (e.g., ManagedIdentityCredential) to avoid
                // latency issues, unintended credential probing, and potential security risks from fallback mechanisms.

                //AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
                //    .GetChatClient(deploymentName)
                //    .AsAIAgent(
                //        instructions: "You are a helpful assistant hosted in Azure Functions.",
                //        name: "HostedAgent");

                // ✅ Step 1: create chat client
                var chatClient = new AzureOpenAIClient(
                        new Uri(endpoint),
                        new DefaultAzureCredential())
                    .GetChatClient(deploymentName)
                    .AsIChatClient();   // ⚠️ THIS LINE IS REQUIRED

                var agent = chatClient.AsAIAgent(
                instructions: "You are a helpful assistant hosted in Azure Functions.",
                name: "HostedAgent"
                );

                // Configure the function app to host the AI agent.
                // This will automatically generate HTTP API endpoints for the agent.
                using IHost app = FunctionsApplication
                    .CreateBuilder(args)
                    .ConfigureFunctionsWebApplication()
                    .ConfigureDurableAgents(options => options.AddAIAgent(agent, timeToLive: TimeSpan.FromHours(1)))
                    .Build();
                app.Run();
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
