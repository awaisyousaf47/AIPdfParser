using Core.Models;

namespace Services.Interfaces;

public interface IRagService
{
    Task<Document> ProcessDocumentAsync(Stream pdfStream, string documentName);
    Task<QueryResponse> QueryDocumentAsync(string documentId, string question);
    Task<string> GenerateSummaryAsync(string documentId);
    Task<Document?> GetDocumentAsync(string documentId);
    Task DeleteDocumentAsync(string documentId);
}