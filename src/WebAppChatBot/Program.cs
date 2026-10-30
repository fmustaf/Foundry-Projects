using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var hotelJson = await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath, "Data", "hotel-knowledge.json"));
var hotelItems = JsonSerializer.Deserialize<List<HotelItem>>(hotelJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

app.MapPost("/api/chat", async (ChatRequest request, IConfiguration config, IHttpClientFactory clients) =>
{
    if (string.IsNullOrWhiteSpace(request.Message)) return Results.BadRequest(new { error = "Message is required." });

    var endpoint = config["Foundry:Endpoint"]?.TrimEnd('/');
    var deployment = config["Foundry:DeploymentName"];
    var apiKey = config["Foundry:ApiKey"];
    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(apiKey))
        return Results.Problem("Foundry configuration is missing. Set Foundry:Endpoint, Foundry:DeploymentName, and Foundry:ApiKey using user-secrets or environment variables.");

    var relevant = hotelItems
        .Select(x => new { Item = x, Score = x.Keywords.Count(k => request.Message.Contains(k, StringComparison.OrdinalIgnoreCase)) })
        .OrderByDescending(x => x.Score).ThenBy(x => x.Item.Category)
        .Take(5).Select(x => x.Item);

    var context = string.Join("\n\n", relevant.Select(x => $"[{x.Id}] {x.Category}: {x.Content}"));
    var system = """
You are Harbor, a concise and friendly hotel concierge for Contoso Harbor Hotel.
Answer only from the HOTEL KNOWLEDGE supplied below. Never invent prices, hours, availability, policies, or reservation details.
If the answer is not present, say you do not have a verified answer and offer hotel staff assistance.
For emergencies, instruct the guest to call local emergency services and hotel staff.
End grounded answers with a short source marker such as [Source: parking-001].

HOTEL KNOWLEDGE:
""" + context;

    var messages = new List<object> { new { role = "system", content = system } };
    foreach (var m in request.History.TakeLast(8)) messages.Add(new { role = m.Role, content = m.Content });
    messages.Add(new { role = "user", content = request.Message });

    // Foundry v1 endpoint: https://<resource>.openai.azure.com/openai/v1/chat/completions
    var url = endpoint.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase)
        ? $"{endpoint}/chat/completions"
        : $"{endpoint}/openai/v1/chat/completions";
    var payload = JsonSerializer.Serialize(new { model = deployment, messages, max_completion_tokens = 500 });
    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
    httpRequest.Headers.Add("api-key", apiKey);
    httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

    var response = await clients.CreateClient().SendAsync(httpRequest);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode) return Results.Problem($"Foundry returned {(int)response.StatusCode}: {body}");

    using var json = JsonDocument.Parse(body);
    var answer = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    return Results.Ok(new { answer });
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", knowledgeItems = hotelItems.Count }));
app.Run();

record ChatRequest(string Message, List<ChatMessage> History);
record ChatMessage(string Role, string Content);
record HotelItem(string Id, string Category, string Content, List<string> Keywords);
