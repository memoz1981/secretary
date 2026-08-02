using Secretary.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Secretary.Api.Controllers;

/// <summary>Text-based stand-in for the real phone call, while the OpenAI Realtime API +
/// telephony bridge integration is deferred (see backend/README.md). Single-turn: each call
/// gets a fresh agent/thread, so conversation memory across requests isn't implemented here —
/// that's expected to be superseded by the real voice session's own state management, not
/// patched onto this stopgap endpoint.</summary>
[ApiController]
[Authorize(Roles = "Agent")]
[Route("api/agent")]
public sealed class AgentTestController : ControllerBase
{
    private readonly PhoneAgentConversationService _conversationService;

    public AgentTestController(PhoneAgentConversationService conversationService) => _conversationService = conversationService;

    public sealed record TestChatRequest(string Message);

    public sealed record TestChatResponse(string Reply);

    [HttpPost("test-chat")]
    public async Task<ActionResult<TestChatResponse>> TestChat(TestChatRequest request, CancellationToken cancellationToken)
    {
        var reply = await _conversationService.RespondAsync(request.Message, cancellationToken);
        return Ok(new TestChatResponse(reply));
    }
}
