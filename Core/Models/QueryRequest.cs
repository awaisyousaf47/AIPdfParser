namespace Core.Models;

public record QueryRequest(
    string DocumentId,
    string Question,
    int MaxResults = 3,
    float RelevanceThreshold = 0.7f
);