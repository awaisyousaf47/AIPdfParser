using Core.Models;

namespace Services.Interfaces;

public interface IPdfProcessor
{
    Task<string> ExtractTextAsync(Stream pdfStream);
    Task<List<Chunk>> ProcessPdfAsync(Stream pdfStream, int chunkSize = 500);
}