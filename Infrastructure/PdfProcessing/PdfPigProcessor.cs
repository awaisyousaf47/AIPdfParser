using Core.Extensions;
using Core.Models;
using Microsoft.Extensions.Logging;
using Services.Interfaces;
using UglyToad.PdfPig;

namespace Infrastructure.PdfProcessing;

public class PdfPigProcessor(ILogger<PdfPigProcessor> logger) : IPdfProcessor
{
    private readonly ILogger<PdfPigProcessor> _logger = logger;

    public Task<string> ExtractTextAsync(Stream pdfStream)
    {
        if (pdfStream == null)
            throw new ArgumentNullException(nameof(pdfStream));

        try
        {
            using var document = PdfDocument.Open(pdfStream);
            var text = string.Join("\n", document.GetPages().Select(p => p.Text));
            return Task.FromResult(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF");
            throw;
        }
    }

    public async Task<List<Chunk>> ProcessPdfAsync(Stream pdfStream, int chunkSize = 500)
    {
        var fullText = await ExtractTextAsync(pdfStream);

        if (string.IsNullOrWhiteSpace(fullText))
        {
            _logger.LogWarning("No text extracted from PDF");
            return [];
        }

        var textChunks = fullText.SplitIntoChunks(chunkSize);

        var chunks = textChunks
            .Select((text, index) => new Chunk(index, text))
            .ToList();

        _logger.LogInformation("Processed PDF into {Count} chunks", chunks.Count);

        return chunks;
    }
}
