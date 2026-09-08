using Core.Models;

namespace Services.Interfaces;

public interface IVectorStore
{
    Task StoreDocumentAsync(Document document, CancellationToken cancellationToken = default);
    Task<List<Chunk>> SearchAsync(string documentId, float[] queryEmbedding, int topK = 3, CancellationToken cancellationToken = default);
    Task<List<VectorSearchResult>> SearchAllAsync(float[] queryEmbedding, int topK = 10, string? documentIdFilter = null, CancellationToken cancellationToken = default);
    Task<Document?> GetDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    Task DeleteChunkAsync(string documentId, int chunkIndex, CancellationToken cancellationToken = default);
    Task<CollectionInfo> GetCollectionInfoAsync(CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(CancellationToken cancellationToken = default);
}

