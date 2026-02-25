namespace QuickStart_Chat_Web.Models
{
    public class ChatViewModel
    {
        public List<ChatMessage> Messages { get; set; } = new();
        public string? AgentName { get; set; }
        public string? AgentId { get; set; }
    }
}