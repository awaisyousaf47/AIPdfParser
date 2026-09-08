using Core.Models;

namespace Services.Interfaces;

public interface IEmbeddingService
{
    ValueTask<float[]> GenerateEmbeddingAsync(string text);
    ValueTask<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts);
}