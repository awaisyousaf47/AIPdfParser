using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Implementations;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController(IRagService ragService) : ControllerBase
{
    private readonly IRagService _ragService = ragService;

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Document>> UploadDocument(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only PDF files are supported");
        }

        await using var stream = file.OpenReadStream();
        var document = await _ragService.ProcessDocumentAsync(stream, file.FileName);

        return Ok(document);
    }

    [HttpGet("{documentId}/summary")]
    public async Task<ActionResult<string>> GetSummary(string documentId)
    {
        var document = await _ragService.GetDocumentAsync(documentId);
        if (document == null)
        {
            return NotFound($"Document {documentId} not found");
        }

        return Ok(await _ragService.GenerateSummaryAsync(documentId));
    }
}