using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using OpenAI;
using OpenAI.Responses;

namespace QuickStart_Chat_Web.Services
{
    public interface IAzureAIService
    {
        Task<string> SendMessageAsync(string message);
        Task<AgentRecord> GetAgentInfoAsync();
    }

    public class AzureAIService : IAzureAIService
    {
        private readonly AIProjectClient _projectClient;
        private readonly string _agentName;
        private AgentRecord? _cachedAgent;

#pragma warning disable OPENAI001
        public AzureAIService(IConfiguration configuration)
        {


            var projectEndpoint = configuration["AzureAI:ProjectEndpoint"]
                ?? throw new InvalidOperationException("AzureAI:ProjectEndpoint not configured");

            _agentName = configuration["AzureAI:AgentName"]
                ?? throw new InvalidOperationException("AzureAI:AgentName not configured");


            _projectClient = new AIProjectClient(
                endpoint: new Uri(projectEndpoint),
                tokenProvider: new DefaultAzureCredential()
            );
//#pragma warning restore OPENAI001
        }

        public async Task<AgentRecord> GetAgentInfoAsync()
        {
            if (_cachedAgent == null)
            {
                _cachedAgent = await Task.Run(() => _projectClient.Agents.GetAgent(_agentName));
            }
            return _cachedAgent;
        }

        public async Task<string> SendMessageAsync(string message)
        {
            var agentRecord = await GetAgentInfoAsync();

            var responseClient = _projectClient.OpenAI.GetProjectResponsesClientForAgent(agentRecord);

            var response = await Task.Run(() => responseClient.CreateResponse(message));

            return response.Value.GetOutputText();
        }
    }
}