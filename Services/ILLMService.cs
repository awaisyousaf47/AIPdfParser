using Core.Models;

namespace Services.Interfaces;

public interface ILLMService
{
    Task<string> GenerateResponseAsync(string prompt);
    Task<string> SummarizeAsync(string text);
    Task<string> AnswerQuestionAsync(string context, string question);
}