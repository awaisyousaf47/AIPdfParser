namespace Core.Models;

public class VectorSearchResult
{
    public Chunk Chunk { get; set; } = null!;
    public float Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}