using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Configuration;

public class OpenAIOptions
{
    /// <summary>
    /// API key. For real OpenAI, this is your platform.openai.com secret key.
    /// For local Ollama, any non-empty placeholder string works (e.g. "ollama") -
    /// it's required by the client but not checked by Ollama's server.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional. Leave empty to use OpenAI's default endpoint (api.openai.com).
    /// Set to "http://localhost:11434/v1" to use a local Ollama instance instead,
    /// or to any other OpenAI-compatible endpoint.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>Model name used for embeddings, e.g. "text-embedding-3-small" (OpenAI) or "nomic-embed-text" (Ollama).</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>Model name used for chat completions, e.g. "gpt-4o-mini" (OpenAI) or "llama3.1" (Ollama).</summary>
    public string ChatModel { get; set; } = "gpt-4o-mini";

    public int MaxTokens { get; set; } = 800;

    public float Temperature { get; set; } = 0.3f;
}
