namespace Core.Models;

public class Document(
    string id,
    string name,
    string fullText,
    DateTime uploadedAt
)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string FullText { get; set; } = fullText;
    public DateTime UploadedAt { get; set; } = uploadedAt;
    public List<Chunk> Chunks { get; set; } = [];

    // C# 13: params collection (params Span<T>)
    public void AddChunks(params Chunk[] chunks) => Chunks.AddRange(chunks);
}