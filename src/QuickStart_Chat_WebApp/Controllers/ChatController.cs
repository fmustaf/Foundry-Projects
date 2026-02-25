using Microsoft.AspNetCore.Mvc;
using QuickStart_Chat_Web.Models;
using QuickStart_Chat_Web.Services;

namespace QuickStart_Chat_Web.Controllers
{
    public class ChatController : Controller
    {
        private readonly IAzureAIService _azureAIService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IAzureAIService azureAIService, ILogger<ChatController> logger)
        {
            _azureAIService = azureAIService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var agent = await _azureAIService.GetAgentInfoAsync();
                var model = new ChatViewModel
                {
                    AgentName = agent.Name,
                    AgentId = agent.Id,
                    Messages = new List<ChatMessage>()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat page");
                return View("Error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { error = "Message cannot be empty" });
                }

                var response = await _azureAIService.SendMessageAsync(request.Message);

                return Json(new
                {
                    success = true,
                    response = response,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { error = "Failed to send message", details = ex.Message });
            }
        }
    }
}