using System.ClientModel;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Services.Interfaces;

namespace Infrastructure.LLM;

/// <summary>
/// Wraps the OpenAI SDK for both embeddings and chat completions.
/// Works against real OpenAI (api.openai.com) or any OpenAI-compatible endpoint
/// (e.g. a local Ollama instance) depending on OpenAIOptions.BaseUrl.
/// Registered once in DI and exposed through both IEmbeddingService and ILLMService
/// so they share the same underlying client.
/// </summary>
public class OpenAIService : IEmbeddingService, ILLMService
{
    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAIService> _logger;
    private readonly ChatClient _chatClient;
    private readonly EmbeddingClient _embeddingClient;

    public OpenAIService(IOptions<OpenAIOptions> options, ILogger<OpenAIService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var credential = new ApiKeyCredential(_options.ApiKey);
        var clientOptions = new OpenAIClientOptions();

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            // Points the SDK at a custom endpoint - e.g. a local Ollama server
            // (http://localhost:11434/v1) instead of api.openai.com.
            clientOptions.Endpoint = new Uri(_options.BaseUrl);
        }

        _chatClient = new ChatClient(_options.ChatModel, credential, clientOptions);
        _embeddingClient = new EmbeddingClient(_options.EmbeddingModel, credential, clientOptions);
    }

    public async ValueTask<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var response = await _embeddingClient.GenerateEmbeddingAsync(text);
            return response.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding");
            throw;
        }
    }

    public async ValueTask<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
    {
        try
        {
            var response = await _embeddingClient.GenerateEmbeddingsAsync(texts.ToList());
            return response.Value.Select(e => e.ToFloats().ToArray()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings");
            throw;
        }
    }

    public async Task<string> GenerateResponseAsync(string prompt)
    {
        return await CompleteChatAsync(
        [
            new UserChatMessage(prompt)
        ]);
    }

    public async Task<string> SummarizeAsync(string text)
    {
        return await CompleteChatAsync(
        [
            new SystemChatMessage("You are a helpful assistant that writes concise, accurate summaries."),
            new UserChatMessage($"Summarize the following text:\n\n{text}")
        ]);
    }

    public async Task<string> AnswerQuestionAsync(string context, string question)
    {
        return await CompleteChatAsync(
        [
            new SystemChatMessage("Answer the question using only the provided context. If the answer isn't contained in the context, say you don't know."),
            new UserChatMessage($"Context:\n{context}\n\nQuestion:\n{question}")
        ]);
    }

    private async Task<string> CompleteChatAsync(List<ChatMessage> messages)
    {
        try
        {
            var chatOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = _options.MaxTokens,
                Temperature = _options.Temperature
            };

            var response = await _chatClient.CompleteChatAsync(messages, chatOptions);
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate chat completion");
            throw;
        }
    }
}
