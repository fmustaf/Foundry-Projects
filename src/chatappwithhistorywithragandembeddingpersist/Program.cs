using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.ClientModel;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace chatapp;

internal class Program
{
    private const string AzureOpenAIDeploymentName = "gpt-35-turbo";
    private const string AzureOpenAIEndpoint = "https://fez-msdn-openai.openai.azure.com/";
    private const string EmbeddingDeploymentName = "text-embedding-ada-002";
    private const string CollectionName = "net7perf";

    private static async Task Main()
    {
        Console.WriteLine(
            "This code demonstrates Retrieval-Augmented Generation (RAG).\n" +
            "It indexes relevant information as embeddings, retrieves the closest matches for a question, " +
            "and supplies only that context to the agent.\n");

        string apiKey = Environment.GetEnvironmentVariable("AI:AzureOpenAI:APIKey")
            ?? throw new InvalidOperationException(
                "The AI:AzureOpenAI:APIKey environment variable must contain the Azure OpenAI API key.");

        var azureOpenAIClient = new AzureOpenAIClient(
            new Uri(AzureOpenAIEndpoint),
            new ApiKeyCredential(apiKey));

        using ILoggerFactory loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });
        ILogger logger = loggerFactory.CreateLogger<Program>();

        AIAgent agent = azureOpenAIClient
            .GetChatClient(AzureOpenAIDeploymentName)
            .AsAIAgent(
                instructions: "You are an AI assistant that helps people find information. " +
                    "Use supplied reference material when it is relevant to the user's question.",
                name: "RagAssistant",
                loggerFactory: loggerFactory);

        var embeddingClient = azureOpenAIClient.GetEmbeddingClient(EmbeddingDeploymentName);

        await using var connection = new SqliteConnection("Data Source=mydata.db");
        await connection.OpenAsync();
        await InitializeDatabaseAsync(connection);

        List<EmbeddingData> embeddings = await LoadEmbeddingsAsync(connection, CollectionName);
        if (embeddings.Count == 0)
        {
            embeddings = await CreateEmbeddingsAsync(embeddingClient, logger);
            if (embeddings.Count > 0)
            {
                await SaveEmbeddingsAsync(connection, CollectionName, embeddings);
                Console.WriteLine($"Generated database with {embeddings.Count} paragraphs.");
            }
        }
        else
        {
            Console.WriteLine($"Found database with {embeddings.Count} entries.");
        }

        var history = new List<ChatTurn>();

        while (true)
        {
            Console.Write("Question: ");
            string? question = Console.ReadLine();
            if (question is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(question))
            {
                continue;
            }

            try
            {
                List<EmbeddingData> relevantResults = [];
                if (embeddings.Count > 0)
                {
                    var embeddingResponse = await embeddingClient.GenerateEmbeddingAsync(question);
                    relevantResults = SearchSimilarEmbeddings(
                        embeddings,
                        embeddingResponse.Value.ToFloats().ToArray(),
                        limit: 3);
                }

                string prompt = BuildPrompt(history, relevantResults, question);

                // A fresh session prevents transient RAG context from becoming part of later turns.
                AgentSession session = await agent.CreateSessionAsync();
                var response = new StringBuilder();
                await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(prompt, session))
                {
                    Console.Write(update);
                    response.Append(update);
                }

                Console.WriteLine();
                history.Add(new ChatTurn(question, response.ToString()));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to answer the current question.");
                Console.WriteLine("The question could not be completed. Check the Azure OpenAI configuration and try again.");
            }

            Console.WriteLine();
        }
    }

    private static async Task InitializeDatabaseAsync(SqliteConnection connection)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS rag_embeddings (
                collection_name TEXT NOT NULL,
                id TEXT NOT NULL,
                content TEXT NOT NULL,
                embedding_json TEXT NOT NULL,
                PRIMARY KEY (collection_name, id)
            );
            CREATE INDEX IF NOT EXISTS ix_rag_embeddings_collection
                ON rag_embeddings (collection_name);
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<EmbeddingData>> LoadEmbeddingsAsync(
        SqliteConnection connection,
        string collectionName)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, content, embedding_json
            FROM rag_embeddings
            WHERE collection_name = $collectionName
            ORDER BY id;
            """;
        command.Parameters.AddWithValue("$collectionName", collectionName);

        var embeddings = new List<EmbeddingData>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            List<float>? embedding = JsonSerializer.Deserialize<List<float>>(reader.GetString(2));
            if (embedding is not null)
            {
                embeddings.Add(new EmbeddingData(reader.GetString(0), reader.GetString(1), embedding));
            }
        }

        return embeddings;
    }

    private static async Task SaveEmbeddingsAsync(
        SqliteConnection connection,
        string collectionName,
        IEnumerable<EmbeddingData> embeddings)
    {
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO rag_embeddings (collection_name, id, content, embedding_json)
            VALUES ($collectionName, $id, $content, $embedding)
            ON CONFLICT(collection_name, id) DO UPDATE SET
                content = excluded.content,
                embedding_json = excluded.embedding_json;
            """;
        command.Parameters.AddWithValue("$collectionName", collectionName);
        command.Parameters.Add("$id", SqliteType.Text);
        command.Parameters.Add("$content", SqliteType.Text);
        command.Parameters.Add("$embedding", SqliteType.Text);

        foreach (EmbeddingData embedding in embeddings)
        {
            command.Parameters["$id"].Value = embedding.Id;
            command.Parameters["$content"].Value = embedding.Text;
            command.Parameters["$embedding"].Value = JsonSerializer.Serialize(embedding.Embedding);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task<List<EmbeddingData>> CreateEmbeddingsAsync(
        OpenAI.Embeddings.EmbeddingClient embeddingClient,
        ILogger logger)
    {
        try
        {
            using var client = new HttpClient();
            string html = await client.GetStringAsync(
                "https://devblogs.microsoft.com/dotnet/performance_improvements_in_net_7");
            string text = WebUtility.HtmlDecode(Regex.Replace(html, @"<[^>]+>|&nbsp;", " "));
            List<string> paragraphs = SplitTextIntoChunks(text, 1024);
            var embeddings = new List<EmbeddingData>(paragraphs.Count);

            for (int index = 0; index < paragraphs.Count; index++)
            {
                var embeddingResponse = await embeddingClient.GenerateEmbeddingAsync(paragraphs[index]);
                embeddings.Add(new EmbeddingData(
                    $"paragraph{index}",
                    paragraphs[index],
                    embeddingResponse.Value.ToFloats().ToArray().ToList()));
            }

            return embeddings;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create the RAG embedding database.");
            Console.WriteLine("Could not fetch and index the reference document. The agent will answer without RAG context.");
            return [];
        }
    }

    private static List<string> SplitTextIntoChunks(string text, int maxChunkSize)
    {
        string normalizedText = Regex.Replace(text, @"\s+", " ").Trim();
        var chunks = new List<string>();

        for (int start = 0; start < normalizedText.Length;)
        {
            int end = Math.Min(start + maxChunkSize, normalizedText.Length);
            if (end < normalizedText.Length)
            {
                int lastSpace = normalizedText.LastIndexOf(' ', end - 1, end - start);
                if (lastSpace > start)
                {
                    end = lastSpace;
                }
            }

            chunks.Add(normalizedText[start..end].Trim());
            start = end;
            while (start < normalizedText.Length && normalizedText[start] == ' ')
            {
                start++;
            }
        }

        return chunks.Where(chunk => chunk.Length > 0).ToList();
    }

    private static List<EmbeddingData> SearchSimilarEmbeddings(
        IEnumerable<EmbeddingData> embeddings,
        IReadOnlyList<float> queryEmbedding,
        int limit) =>
        embeddings
            .Select(embedding => new
            {
                Data = embedding,
                Similarity = CosineSimilarity(queryEmbedding, embedding.Embedding)
            })
            .OrderByDescending(result => result.Similarity)
            .Take(limit)
            .Select(result => result.Data)
            .ToList();

    private static double CosineSimilarity(IReadOnlyList<float> first, IReadOnlyList<float> second)
    {
        if (first.Count != second.Count)
        {
            return 0;
        }

        double dotProduct = 0;
        double firstMagnitude = 0;
        double secondMagnitude = 0;

        for (int index = 0; index < first.Count; index++)
        {
            dotProduct += first[index] * second[index];
            firstMagnitude += first[index] * first[index];
            secondMagnitude += second[index] * second[index];
        }

        double denominator = Math.Sqrt(firstMagnitude) * Math.Sqrt(secondMagnitude);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }

    private static string BuildPrompt(
        IEnumerable<ChatTurn> history,
        IEnumerable<EmbeddingData> relevantResults,
        string question)
    {
        var prompt = new StringBuilder();

        foreach (ChatTurn turn in history)
        {
            prompt.AppendLine($"User: {turn.Question}");
            prompt.AppendLine($"Assistant: {turn.Answer}");
        }

        List<EmbeddingData> context = relevantResults.ToList();
        if (context.Count > 0)
        {
            prompt.AppendLine("Reference material for this question:");
            foreach (EmbeddingData result in context)
            {
                prompt.AppendLine(result.Text);
            }
        }

        prompt.AppendLine($"User question: {question}");
        return prompt.ToString();
    }

    private sealed record EmbeddingData(string Id, string Text, List<float> Embedding);

    private sealed record ChatTurn(string Question, string Answer);
}
