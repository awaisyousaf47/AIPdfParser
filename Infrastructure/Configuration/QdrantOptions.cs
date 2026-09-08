namespace Infrastructure.Configuration;

public class QdrantOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6333;
    public bool UseHttps { get; set; } = false;
    public string? ApiKey { get; set; }
    public string CollectionName { get; set; } = "documents";
    public int VectorSize { get; set; } = 1536; // For text-embedding-ada-002
    public string DistanceMetric { get; set; } = "Cosine";
    public int DefaultLimit { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 30;
}