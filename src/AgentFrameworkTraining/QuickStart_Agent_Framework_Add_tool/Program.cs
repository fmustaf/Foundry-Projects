// Copyright (c) Microsoft. All rights reserved.

// This sample demonstrates how to use a ChatClientAgent with function tools.
// It shows both non-streaming and streaming agent interactions using menu-related tools.

using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace QuickStart_Agent_Framework_Sample_One
{
    public class Program
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

            [Description("Get the weather for a given location.")]
            static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

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

                AIAgent agent = new AzureOpenAIClient(
                    new Uri(endpoint),
                    new DefaultAzureCredential())
                    //new Azure.AzureKeyCredential(apiKey))
                    .GetChatClient(deploymentName)
                    .AsAIAgent(instructions: "You are a helpful assistant.", tools: [AIFunctionFactory.Create(GetWeather)]);

                // Non-streaming agent interaction with function tools.
                Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?"));

                // Invoke the agent with streaming support.
                await foreach (var update in agent.RunStreamingAsync("What is the weather like in Amsterdam?"))
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
