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

    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Question is required.", ct);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/plain; charset=utf-8";

        await foreach (var chunk in _chat.StreamAnswerAsync(req, ct))
        {
            if (string.IsNullOrEmpty(chunk))
                continue;

            await Response.WriteAsync(chunk, ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    [HttpPost("sources")]
    [ProducesResponseType(typeof(IReadOnlyList<SourceHit>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SourceHit>>> Sources([FromBody] ChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest("Question is required.");

        var sources = await _chat.GetSourcesAsync(req, ct);
        return Ok(sources);
    }

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