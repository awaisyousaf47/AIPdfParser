using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Services.Implementations;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryController(IRagService ragService) : ControllerBase
{
    private readonly IRagService _ragService = ragService;

    [HttpPost]
    public async Task<ActionResult<QueryResponse>> QueryDocument([FromBody] QueryRequest request)
    {
        if (string.IsNullOrEmpty(request.Question))
        {
            return BadRequest("Question cannot be empty");
        }

        var response = await _ragService.QueryDocumentAsync(request.DocumentId, request.Question);
        return Ok(response);
    }
}