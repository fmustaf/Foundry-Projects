using System.Reflection.Metadata;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using OpenAI;
using OpenAI.Responses;
using System.ClientModel.Primitives;

namespace Chat_WithModel_No_Agent
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

#pragma warning disable OPENAI001

            const string deploymentName = "gpt-4.1-mini";
            const string endpoint = "https://demofaisalnewui-1423-resource.openai.azure.com/openai/v1/";


            BearerTokenPolicy tokenPolicy = new(
                new DefaultAzureCredential(),
                "https://cognitiveservices.azure.com/.default");

            ResponsesClient client = new(
                model: deploymentName,
                authenticationPolicy: tokenPolicy,
                options: new OpenAIClientOptions()
                {
                    Endpoint = new Uri($"{endpoint}"),
                });
            CreateResponseOptions options = new()
            {
                Temperature = (float)0.7,
                InputItems =
    {
        ResponseItem.CreateUserMessageItem("What is the size of France in square miles?"),
        ResponseItem.CreateUserMessageItem("And what is the capital city?"),

    },
            };



            ResponseResult response = client.CreateResponse(options);

            Console.WriteLine($"[ASSISTANT]: {response.GetOutputText()}");

        }
    }
}
