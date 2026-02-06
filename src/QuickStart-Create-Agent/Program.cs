using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
//using OpenAI;
//using OpenAI.Responses;

namespace QuickStart_Create_Agent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

#pragma warning disable OPENAI001
            Console.WriteLine("Now, let's create an agent from the code in Microsoft Foundry!");

            const string projectEndpoint = "https://demofaisalnewui-1423-resource.services.ai.azure.com/api/projects/demofaisalnewui-1423";
            const string agentName = "AgentFez007";
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

            AgentDefinition agentDefinition = new PromptAgentDefinition(modelDeploymentName)
            {
                Instructions = "You are a helpful assistant that answers general questions",
            };

            AgentVersion newAgentVersion = projectClient.Agents.CreateAgentVersion(
                agentName,
                options: new(agentDefinition));

            var agentVersionsResult = projectClient.Agents.GetAgentVersions(agentName);
            List<AgentVersion> agentVersions = agentVersionsResult.ToList();
            foreach (AgentVersion agentVersion in agentVersions)
            {
                Console.WriteLine($"Agent: {agentVersion.Id}, Name: {agentVersion.Name}, Version: {agentVersion.Version}");
            }
        }
    }
}
