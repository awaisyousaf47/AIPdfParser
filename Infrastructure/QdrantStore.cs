using Core.Models;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Services.Interfaces;
using System.Collections.Concurrent;
using static Qdrant.Client.Grpc.Conditions;

namespace Infrastructure.VectorDB;

public class QdrantStore : IVectorStore, IDisposable
{
    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantStore> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;
    private bool _collectionInitialized;

    public QdrantStore(
        IOptions<QdrantOptions> options,
        ILogger<QdrantStore> logger)
    {
        _options = options.Value;
        _logger = logger;

        _client = new QdrantClient(
            host: _options.Host,
            port: _options.Port,
            https: _options.UseHttps,
            apiKey: _options.ApiKey
        );

        _logger.LogInformation("QdrantStore initialized with host: {Host}:{Port}",
            _options.Host, _options.Port);
    }

    /// <summary>
    /// Initialize the collection if it doesn't exist
    /// </summary>
    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_collectionInitialized)
            return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_collectionInitialized)
                return;

            var collections = await _client.ListCollectionsAsync(cancellationToken);

            if (!collections.Contains(_options.CollectionName))
            {
                _logger.LogInformation("Creating collection: {CollectionName}", _options.CollectionName);

                await _client.CreateCollectionAsync(
                    collectionName: _options.CollectionName,
                    vectorsConfig: new VectorParams
                    {
                        Size = (ulong)_options.VectorSize,
                        Distance = GetDistanceMetric()
                    },
                    cancellationToken: cancellationToken
                );

                // Create indexes for faster filtering
                await _client.CreatePayloadIndexAsync(
                    collectionName: _options.CollectionName,
                    fieldName: "document_id",
                    schemaType: PayloadSchemaType.Keyword,
                    cancellationToken: cancellationToken
                );

                await _client.CreatePayloadIndexAsync(
                    collectionName: _options.CollectionName,
                    fieldName: "chunk_index",
                    schemaType: PayloadSchemaType.Integer,
                    cancellationToken: cancellationToken
                );

            }
            else
            {
                _logger.LogInformation("Collection already exists: {CollectionName}", _options.CollectionName);
            }

            _collectionInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure collection exists");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Store a document with all its chunks and embeddings
    /// </summary>
    public async Task StoreDocumentAsync(Core.Models.Document document, CancellationToken cancellationToken = default)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (string.IsNullOrEmpty(document.Id))
            throw new ArgumentException("Document ID cannot be empty", nameof(document));

        if (document.Chunks == null || document.Chunks.Count == 0)
            throw new ArgumentException("Document must have at least one chunk", nameof(document));

        await EnsureCollectionExistsAsync(cancellationToken);

        try
        {
            // Prepare points for upsert
            var points = new List<PointStruct>();

            foreach (var chunk in document.Chunks)
            {
                if (chunk.Embedding == null || chunk.Embedding.Length == 0)
                {
                    _logger.LogWarning("Chunk {Index} has no embedding, skipping", chunk.Index);
                    continue;
                }

                var pointId = Guid.NewGuid();

                var point = new PointStruct
                {
                    Id = pointId,
                    Vectors = chunk.Embedding,
                    Payload =
                        {
                            ["document_id"] = document.Id,
                            ["document_name"] = document.Name,
                            ["chunk_index"] = chunk.Index,
                            ["text"] = chunk.Text,
                            ["uploaded_at"] = document.UploadedAt.ToString("o"),
                            ["chunk_id"] = $"{document.Id}-{chunk.Index}"
                        }
                };

                points.Add(point);
            }

            if (points.Count == 0)
            {
                _logger.LogWarning("No valid points to store for document {DocumentId}", document.Id);
                return;
            }

            // Upsert points in batches to avoid timeout
            const int batchSize = 100;
            for (int i = 0; i < points.Count; i += batchSize)
            {
                var batch = points.Skip(i).Take(batchSize).ToList();
                await _client.UpsertAsync(
                    collectionName: _options.CollectionName,
                    points: batch,
                    cancellationToken: cancellationToken
                );

                _logger.LogDebug("Upserted batch {BatchNumber} with {Count} points",
                    i / batchSize + 1, batch.Count);
            }

            _logger.LogInformation("Stored document {DocumentId} with {Count} chunks",
                document.Id, points.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store document {DocumentId}", document.Id);
            throw;
        }
    }

    /// <summary>
    /// Search for relevant chunks using vector similarity
    /// </summary>
    public async Task<List<Chunk>> SearchAsync(
        string documentId,
        float[] queryEmbedding,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(documentId))
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));

        if (queryEmbedding == null || queryEmbedding.Length == 0)
            throw new ArgumentException("Query embedding cannot be empty", nameof(queryEmbedding));

        await EnsureCollectionExistsAsync(cancellationToken);

        try
        {
            // Build filter to only search within the specific document
            var filter = MatchKeyword("document_id", documentId);

            // Perform the search
            var searchResult = await _client.SearchAsync(
                collectionName: _options.CollectionName,
                vector: queryEmbedding,
                limit: (ulong)topK,
                filter: filter,
                cancellationToken: cancellationToken
            );

            var chunks = new List<Chunk>();

            foreach (var scoredPoint in searchResult)
            {
                var payload = scoredPoint.Payload;

                // Extract text and other metadata
                var text = payload.TryGetValue("text", out var textObj)
                    ? GetString(textObj)
                    : string.Empty;

                var chunkIndex = payload.TryGetValue("chunk_index", out var indexObj)
                    ? GetInt(indexObj)
                    : 0;

                // Extract embedding if available (for debugging)
                // Note: Qdrant doesn't return vectors by default unless requested

                var chunk = new Chunk(
                    chunkIndex,
                    text,
                    null // We don't return embeddings in search by default to save bandwidth
                );

                chunks.Add(chunk);

                _logger.LogDebug("Found chunk {Index} with score {Score}",
                    chunkIndex, scoredPoint.Score);
            }

            _logger.LogInformation("Found {Count} relevant chunks for document {DocumentId}",
                chunks.Count, documentId);

            return chunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search document {DocumentId}", documentId);
            throw;
        }
    }

    /// <summary>
    /// Search across all documents (with optional filter)
    /// </summary>
    public async Task<List<VectorSearchResult>> SearchAllAsync(
        float[] queryEmbedding,
        int topK = 10,
        string? documentIdFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding == null || queryEmbedding.Length == 0)
            throw new ArgumentException("Query embedding cannot be empty", nameof(queryEmbedding));

        await EnsureCollectionExistsAsync(cancellationToken);

        try
        {
            // Build filter if document ID is provided
            Filter? filter = null;
            if (!string.IsNullOrEmpty(documentIdFilter))
            {
                filter = MatchKeyword("document_id", documentIdFilter);
            }

            var searchResult = await _client.SearchAsync(
                collectionName: _options.CollectionName,
                vector: queryEmbedding,
                limit: (ulong)topK,
                filter: filter,
                cancellationToken: cancellationToken
            );

            var results = new List<VectorSearchResult>();

            foreach (var scoredPoint in searchResult)
            {
                var payload = scoredPoint.Payload;

                var text = payload.TryGetValue("text", out var textObj)
                    ? GetString(textObj)
                    : string.Empty;

                var chunkIndex = payload.TryGetValue("chunk_index", out var indexObj)
                    ? GetInt(indexObj)
                    : 0;

                var docId = payload.TryGetValue("document_id", out var docIdObj)
                    ? GetString(docIdObj)
                    : string.Empty;

                var docName = payload.TryGetValue("document_name", out var docNameObj)
                    ? GetString(docNameObj)
                    : string.Empty;

                var metadata = new Dictionary<string, object>
                {
                    ["document_id"] = docId,
                    ["document_name"] = docName,
                    ["chunk_index"] = chunkIndex
                };

                var chunk = new Chunk(chunkIndex, text);

                results.Add(new VectorSearchResult
                {
                    Chunk = chunk,
                    Score = scoredPoint.Score,
                    Metadata = metadata
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search across documents");
            throw;
        }
    }

    /// <summary>
    /// Get a document with all its chunks
    /// </summary>
    public async Task<Core.Models.Document?> GetDocumentAsync(
    string documentId,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(documentId))
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));

        await EnsureCollectionExistsAsync(cancellationToken);

        try
        {
            // First, get all points for this document
            var filter = MatchKeyword("document_id", documentId);

            // Use scroll to get all points
            var scrollResult = await _client.ScrollAsync(
                collectionName: _options.CollectionName,
                filter: filter,
                limit: 1000, // Adjust based on expected chunks per document
                cancellationToken: cancellationToken
            );

            var points = scrollResult.Result.ToList();

            if (points.Count == 0)
            {
                _logger.LogDebug("No chunks found for document {DocumentId}", documentId);
                return null;
            }

            // Extract document metadata from first point
            var firstPayload = points.First().Payload;
            var documentName = firstPayload.TryGetValue("document_name", out var nameObj)
                ? GetString(nameObj, "Unknown")
                : "Unknown";

            var uploadedAtStr = firstPayload.TryGetValue("uploaded_at", out var dateObj)
                ? GetString(dateObj, DateTime.UtcNow.ToString("o"))
                : DateTime.UtcNow.ToString("o");

            var uploadedAt = DateTime.Parse(uploadedAtStr);

            // Build chunks
            var chunks = new List<Chunk>();
            var fullText = new List<string>();

            foreach (var point in points.OrderBy(p =>
                GetInt(p.Payload.TryGetValue("chunk_index", out var idxVal) ? idxVal : null)))
            {
                var text = point.Payload.TryGetValue("text", out var textObj)
                    ? GetString(textObj)
                    : string.Empty;

                var chunkIndex = point.Payload.TryGetValue("chunk_index", out var chunkIdxObj)
                    ? GetInt(chunkIdxObj)
                    : 0;

                // Note: We could retrieve vectors but it's expensive
                chunks.Add(new Chunk(chunkIndex, text, null));
                fullText.Add(text);
            }

            // Reconstruct document
            var document = new Core.Models.Document(
                documentId,
                documentName,
                string.Join("\n", fullText),
                uploadedAt
            );
            document.AddChunks([.. chunks]);

            _logger.LogInformation("Retrieved document {DocumentId} with {Count} chunks",
                documentId, chunks.Count);

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document {DocumentId}", documentId);
            throw;
        }
    }

    /// <summary>
    /// Delete a document and all its chunks
    /// </summary>
    public async Task DeleteDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(documentId))
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));

        await EnsureCollectionExistsAsync(cancellationToken);

        try
        {
            // Delete all points with the document_id
            var filter = MatchKeyword("document_id", documentId);

            await _client.DeleteAsync(
                collectionName: _options.CollectionName,
                filter: filter,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Deleted document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId}", documentId);
            throw;
        }
    }

    /// <summary>
    /// Delete a specific chunk from a document
    /// </summary>
    public async Task DeleteChunkAsync(
        string documentId,
        int chunkIndex,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(documentId))
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));

        await EnsureCollectionExistsAsync(cancellationToken);

        try
        {
            // Build filter for specific document and chunk
            var filter = MatchKeyword("document_id", documentId) &
                        MatchKeyword("chunk_index", chunkIndex.ToString());

            await _client.DeleteAsync(
                collectionName: _options.CollectionName,
                filter: filter,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Deleted chunk {ChunkIndex} from document {DocumentId}",
                chunkIndex, documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chunk {ChunkIndex} from document {DocumentId}",
                chunkIndex, documentId);
            throw;
        }
    }

    /// <summary>
    /// Get collection statistics
    /// </summary>
    public async Task<Core.Models.CollectionInfo> GetCollectionInfoAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        try
        {
            var info = await _client.GetCollectionInfoAsync(
                collectionName: _options.CollectionName,
                cancellationToken: cancellationToken
            );

            return new Core.Models.CollectionInfo
            {
                Name = _options.CollectionName,
                PointsCount = (int)info.PointsCount,
                VectorsCount = (int)info.SegmentsCount,
                IndexedVectorsCount = (int)info.IndexedVectorsCount,
                Status = info.Status.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collection info");
            throw;
        }
    }

    /// <summary>
    /// Delete the entire collection (use with caution!)
    /// </summary>
    public async Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteCollectionAsync(
                collectionName: _options.CollectionName,
                cancellationToken: cancellationToken
            );

            _collectionInitialized = false;
            _logger.LogWarning("Deleted entire collection: {CollectionName}", _options.CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete collection");
            throw;
        }
    }

    /// <summary>
    /// Helper: Get the distance metric from configuration
    /// </summary>
    private Distance GetDistanceMetric()
    {
        return _options.DistanceMetric?.ToLowerInvariant() switch
        {
            "cosine" => Distance.Cosine,
            "dot" => Distance.Dot,
            "euclidean" => Distance.Euclid,
            "manhattan" => Distance.Manhattan,
            _ => Distance.Cosine
        };
    }

    private static string GetString(Value? value, string fallback = "") =>
    value != null && value.KindCase == Value.KindOneofCase.StringValue
        ? value.StringValue
        : fallback;

    private static int GetInt(Value? value, int fallback = 0) =>
        value != null && value.KindCase == Value.KindOneofCase.IntegerValue
            ? (int)value.IntegerValue
            : fallback;

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _client?.Dispose();
        _semaphore?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
