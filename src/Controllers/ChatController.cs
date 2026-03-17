using AiAssistant.Api.Contracts;
using AiAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ChatController : ControllerBase
{
    private readonly ChatService _chat;

    public ChatController(ChatService chat)
    {
        _chat = chat;
    }

    /// <summary>
    /// Stateless chat endpoint (no database).
    /// 
    /// Uses Azure AI Search for retrieval and Azure OpenAI for generation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest("Question is required.");

        var result = await _chat.AskAsync(req, ct);
        return Ok(result);
    }
}
