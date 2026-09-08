namespace Core.Models;

public record Chunk(
    int Index,
    string Text,
    float[]? Embedding = null
)
{
    public string Id => $"{Index}";
    public int TokenCount => Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}