using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using OpenAI;
using OpenAI.Responses;

using System;
using System.Threading.Tasks;

namespace QuickStart_Chat_With_Model
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("This sample uses just the Projects SDK, aka, Azure Foundry SDK.");

#pragma warning disable OPENAI001

            const string projectEndpoint = "https://demofaisalnewui-1423-resource.services.ai.azure.com/api/projects/demofaisalnewui-1423";
            const string agentName = "AgentBond";

            // Connect to your project using the endpoint from your project page
            // Using the DefaultAzureCredential, which supports various authentication methods (environment variables, managed identity, etc.).
            // Ensure you are logged into Azure using Az Login or have the appropriate environment variables set for authentication.
            AIProjectClient projectClient = new(endpoint: new Uri(projectEndpoint), tokenProvider: new DefaultAzureCredential());

            AgentRecord agentRecord = projectClient.Agents.GetAgent(agentName);
            Console.WriteLine($"Agent retrieved (name: {agentRecord.Name}, id: {agentRecord.Id})");

            // Initialize a client reference            
            ProjectResponsesClient responseClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentRecord);
            // Use the agent to generate a response
            var response = responseClient.CreateResponse("Hello! Tell me some cool Dad jokes.");

            Console.WriteLine(response.Value.GetOutputText());
        }
    }
}
