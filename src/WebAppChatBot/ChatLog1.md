## :bust_in_silhouette:  (10/29/2026 4:55 PM)
Explain to me what framework is the code using to communicate with Microsoft Foundry, identify the using statments. I do not see Agent Frmework in the dependencies
## :speech_balloon: GitHub Copilot
1. skill(microsoft-foundry) :white_check_mark:
1. Run command: & 'C:\Users\faisalm\.claude\skills\microsoft-foundry\scripts\check-and-setup-... :white_check_mark:

1. Read: Program.cs :white_check_mark:
1. Find files: **/*.{csproj,props,targets,json} :white_check_mark:

1. Read: WebAppChatBot.csproj :white_check_mark:
1. Read: appsettings.json :white_check_mark:
This code communicates with Microsoft Foundry using **direct HTTP requests** from .NET’s `HttpClient`, targeting Foundry’s OpenAI-compatible **Chat Completions** endpoint (`/openai/v1/chat/completions`). It is an ASP.NET Core Minimal API app, but it does **not** use the Microsoft Agent Framework or a Foundry Agent SDK.

The explicit `using` statements in `Program.cs` are:

- `using System.Net.Http.Headers;` — appears unused in this file.
- `using System.Text;` — used for UTF-8 request content.
- `using System.Text.Json;` — used to serialize the request and parse the response.

`System.Net.Http` is available through the project’s enabled implicit usings. The `.csproj` has no package references; the code builds the JSON payload, sends it with `HttpClient`, and authenticates using the configured API key.

___

## :bust_in_silhouette:
Do you recommend updating this code to use Agent Framework? This is what we advise our customers
## :speech_balloon: GitHub Copilot
1. Web search: Current Microsoft Agent Framework .NET documentation integration with Microso... :white_check_mark:

1. Fetch URL: https://learn.microsoft.com/en-us/agent-framework/overview/ :white_check_mark:
1. Fetch URL: https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/m... :white_check_mark:
**Given that you advise customers to use Microsoft Agent Framework, I’d update this sample** so the code demonstrates the recommended approach. The current app is a simple HTTP client calling Chat Completions directly; it doesn’t use Agent Framework.

For this kind of app, the relevant pattern is a **code-first Agent Framework agent** connected to a Foundry project. Your app still owns the instructions, knowledge context, and tools; this does **not** make it a Foundry-managed agent. The framework would give you a standard agent/session abstraction and a clearer path to adding tools or other agent capabilities. The tradeoff is extra SDK dependencies and a change from the current API-key/direct-endpoint setup to the Foundry project integration and its authentication pattern.

If the goal is only to show the smallest possible chat-completions call, the direct HTTP approach is simpler. But for a customer-facing reference aligned with your guidance, I’d choose Agent Framework. The current Microsoft Learn example uses `Microsoft.Agents.AI.Foundry` and the namespaces `Azure.AI.Projects`, `Azure.Identity`, and `Microsoft.Agents.AI`:

- [Microsoft Agent Framework overview](https://learn.microsoft.com/en-us/agent-framework/overview/)
- [Microsoft Foundry provider for Agent Framework](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/microsoft-foundry)

___

## :bust_in_silhouette:
Find where this string is set for the web page "Contoso Harbor Hotel"
## :speech_balloon: GitHub Copilot

1. Failed: search Contoso Harbor Hotel :negative_squared_cross_mark:
1. Search: Harbor Hotel|<title>|title :white_check_mark:
The page heading **“Contoso Harbor Hotel”** is set in `wwwroot\index.html`, inside the header’s `<h1>`:
```html
<header><h1>Contoso Harbor Hotel</h1>
```

The browser tab title is separately set to **“Harbor Concierge”** in that file’s `<title>` element.

___

## :bust_in_silhouette:
Explain to me how this code is using the endpoint, deployment, apikey to answer the questions being asked on the website. I want to understand step by step what happens when a question is asked and how the model deployment in Foundry responds
## :speech_balloon: GitHub Copilot
1. Read: app.js :white_check_mark:
Here’s the request flow, from the website to the Foundry deployment and back:

1. **The browser sends the question.** In `wwwroot\app.js`, submitting the chat form or clicking a suggested question calls `POST /api/chat` with the question and the conversation history. The browser shows a “Checking hotel information...” indicator while it waits.

2. **The server reads Foundry settings.** The handler in `Program.cs` gets `Foundry:Endpoint`, `Foundry:DeploymentName`, and `Foundry:ApiKey` from ASP.NET Core configuration. The sample `appsettings.json` has these values blank, so they need to be supplied at runtime, such as through user secrets or environment variables. If any are missing, the server returns an error instead of calling Foundry.

3. **The server selects hotel information.** It compares the question against the keywords in `Data\hotel-knowledge.json`, ranks the entries by matches, and takes up to five. It places those entries in the system instructions along with rules to answer from that information and not invent details. This is a lightweight keyword-based retrieval step—not a separate Foundry search service.

4. **The server builds the model request.** It sends the system instructions, the last eight history messages, and the current question as a JSON chat-completions request. The `DeploymentName` is sent as the `model` value, telling the endpoint which model deployment to use. The URL is formed as `<Endpoint>/openai/v1/chat/completions` (or appends `/chat/completions` if the endpoint already ends in `/openai/v1`).

5. **The API key authenticates the call.** The server adds the key in the `api-key` HTTP header, then posts the request to Foundry. The key is used by the server; it is not sent from the browser.

6. **Foundry returns a completion.** The deployed model processes the instructions, hotel context, and conversation, then returns a JSON response. The code reads `choices[0].message.content` as the answer. If Foundry responds with an error status, the server returns an error instead of treating it as an answer.

7. **The browser displays the answer.** The server returns `{ answer: ... }`; `app.js` displays it and adds the question and answer to the in-memory history for subsequent turns.

In short: **the endpoint identifies where to send the request, the deployment name selects which model deployment handles it, and the API key authenticates the server’s request.** The hotel knowledge and response rules are supplied by this application in each request; they are not automatically retrieved by the model from Foundry.
