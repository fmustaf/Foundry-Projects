using System.Reflection.Metadata;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using OpenAI;
using OpenAI.Responses;

namespace QuickStart_Chat_With_Agent
{
    internal class Program
    {
        static void Main(string[] args)
        {
            #pragma warning disable OPENAI001
            Console.WriteLine("We programatically created the Agent in Microsoft Foundry, lets chat with it!!");

            const string projectEndpoint = "https://demofaisalnewui-1423-resource.services.ai.azure.com/api/projects/demofaisalnewui-1423";
            const string agentName = "AgentFez";
            string modelDeploymentName = "gpt-4.1-mini";

            //string projectEndpoint = Environment.GetEnvironmentVariable("PROJECT_ENDPOINT")
            //    ?? throw new InvalidOperationException("Missing environment variable 'PROJECT_ENDPOINT'");
            //string modelDeploymentName = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME")
            //    ?? throw new InvalidOperationException("Missing environment variable 'MODEL_DEPLOYMENT_NAME'");
            //string agentName = Environment.GetEnvironmentVariable("AGENT_NAME")
            //    ?? throw new InvalidOperationException("Missing environment variable 'AGENT_NAME'");

            // Connect to your project using the endpoint from your project page
            // The AzureCliCredential will use your logged-in Azure CLI identity, make sure to run `az login` first
            // You can use the Developer PowerShell window for this, which has Azure CLI installed and configured.
            AIProjectClient projectClient = new(new Uri(projectEndpoint), new AzureCliCredential());

            // Optional Step: Create a conversation to use with the agent
            ProjectConversation conversation = projectClient.OpenAI.Conversations.CreateProjectConversation();

            ProjectResponsesClient responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: agentName,
                defaultConversationId: conversation.Id);


            // Chat with the agent to answer questions
            ResponseResult response = responsesClient.CreateResponse("What is the size of France in square miles?");
            Console.WriteLine(response.GetOutputText());

            // Optional Step: Ask a follow-up question in the same conversation
            response = responsesClient.CreateResponse("And what is the capital city?");
            Console.WriteLine(response.GetOutputText());
        }
    }
}
