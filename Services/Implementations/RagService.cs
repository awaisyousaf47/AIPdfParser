using Core.Models;
using Services.Interfaces;

namespace Services.Implementations;

public class RagService(
    IPdfProcessor pdfProcessor,
    IEmbeddingService embeddingService,
    IVectorStore vectorStore,
    ILLMService llmService
) : IRagService
{
    private readonly IPdfProcessor _pdfProcessor = pdfProcessor;
    private readonly IEmbeddingService _embeddingService = embeddingService;
    private readonly IVectorStore _vectorStore = vectorStore;
    private readonly ILLMService _llmService = llmService;

    public Task DeleteDocumentAsync(string documentId)
    {
        return _vectorStore.DeleteDocumentAsync(documentId);
    }

    public async Task<string> GenerateSummaryAsync(string documentId)
    {
        var document = await _vectorStore.GetDocumentAsync(documentId)
            ?? throw new InvalidOperationException($"Document {documentId} not found");

        return await _llmService.SummarizeAsync(document.FullText);
    }

    public Task<Document?> GetDocumentAsync(string documentId)
    {
        return _vectorStore.GetDocumentAsync(documentId);
    }

    public async Task<Document> ProcessDocumentAsync(Stream pdfStream, string documentName)
    {
        // Extract and chunk
        var chunks = await _pdfProcessor.ProcessPdfAsync(pdfStream);

        // Generate embeddings
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
            chunks.Select(c => c.Text)
        );

        // Update chunks with embeddings
        var chunksWithEmbeddings = chunks
            .Select((chunk, index) => chunk with { Embedding = embeddings[index] })
            .ToList();

        // Create document
        var document = new Document(
            Guid.NewGuid().ToString(),
            documentName,
            string.Join("\n", chunks.Select(c => c.Text)),
            DateTime.UtcNow
        );
        document.AddChunks([.. chunksWithEmbeddings]);

        // Store
        await _vectorStore.StoreDocumentAsync(document);

        return document;
    }

    public async Task<QueryResponse> QueryDocumentAsync(string documentId, string question)
    {
        var startTime = DateTime.UtcNow;

        // Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(question);

        // Search for relevant chunks
        var relevantChunks = await _vectorStore.SearchAsync(documentId, queryEmbedding);

        // Build context
        var context = string.Join("\n\n", relevantChunks.Select(c => c.Text));

        // Get answer from LLM
        var answer = await _llmService.AnswerQuestionAsync(context, question);

        return new QueryResponse(
            answer,
            relevantChunks,
            0.95f, // Would calculate actual confidence in production
            (DateTime.UtcNow - startTime).Milliseconds
        );
    }
}