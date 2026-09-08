namespace Core.Extensions;

public static class StringExtensions
{
    public static string Truncate(this string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    // C# 13: using collection expressions for defaults
    public static string[] SplitIntoChunks(this string text, int chunkSize = 500, int overlap = 50)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        for (int i = 0; i < words.Length; i += chunkSize - overlap)
        {
            var chunk = string.Join(' ', words[i..Math.Min(i + chunkSize, words.Length)]);
            result.Add(chunk);
        }

        return [.. result]; // Collection expression
    }
}