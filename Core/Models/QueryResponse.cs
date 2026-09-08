namespace Core.Models;

public record QueryResponse(
    string Answer,
    List<Chunk> SourceChunks,
    float ConfidenceScore,
    long ProcessingTimeMs
);