# WebAppChatBot
ASP.NET Core 8 hospitality chatbot using an existing Microsoft Foundry model deployment.

## Configure safely
From the project folder:

dotnet user-secrets init
dotnet user-secrets set "Foundry:Endpoint" "https://YOUR-RESOURCE.openai.azure.com"
dotnet user-secrets set "Foundry:DeploymentName" "YOUR-DEPLOYMENT-NAME"
dotnet user-secrets set "Foundry:ApiKey" "YOUR-API-KEY"

Then run: dotnet run
Open: http://localhost:5078
