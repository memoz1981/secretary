using Secretary.Api.Auth;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Api.Controllers;

/// <summary>Queueing a survey call, and reading what came back.
///
/// The queue endpoint is the seam this whole module is built around. Today a person fills in a
/// name and a number and clicks; when telephony lands, the scheduler posts exactly this and
/// something dials the number already in the row. Nothing downstream changes.</summary>
[ApiController]
[RequireModule(Module.Feedback)]
[Authorize(Roles = "Owner,Staff")]
[Route("api/feedback/calls")]
public sealed class FeedbackCallsController : ControllerBase
{
    private readonly FeedbackCallService _calls;

    public FeedbackCallsController(FeedbackCallService calls) => _calls = calls;

    /// <summary>Records who is about to be rung, and returns the id the call is dialled with.</summary>
    [HttpPost]
    public async Task<ActionResult<FeedbackCallResponse>> Queue(
        QueueFeedbackCallRequest request, CancellationToken cancellationToken)
        => Ok(await _calls.QueueAsync(request, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FeedbackCallResponse>>> Search(
        [FromQuery] Instant? from, [FromQuery] Instant? to, [FromQuery] int? surveyId,
        CancellationToken cancellationToken)
        => Ok(await _calls.SearchAsync(from, to, surveyId, cancellationToken));

    /// <summary>The people rather than the dials. One row per person however many times we rang
    /// them, which is what the follow-up list is for.</summary>
    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<SurveyRequestResponse>>> Requests(
        [FromQuery] Instant? from, [FromQuery] Instant? to, [FromQuery] int? surveyId,
        CancellationToken cancellationToken)
        => Ok(await _calls.SearchRequestsAsync(from, to, surveyId, cancellationToken));

    /// <summary>Ring somebody again. The same method a scheduler will call for a due retry, so a
    /// manual attempt and an automatic one cannot be counted differently.</summary>
    [HttpPost("requests/{requestId:int}/retry")]
    public async Task<ActionResult<FeedbackCallResponse>> Retry(
        int requestId, CancellationToken cancellationToken)
        => Ok(await _calls.RetryAsync(requestId, cancellationToken));

    /// <summary>Stop chasing somebody, without recording a survey that never happened.</summary>
    [HttpPost("requests/{requestId:int}/close")]
    public async Task<IActionResult> Close(int requestId, CancellationToken cancellationToken)
    {
        await _calls.CloseRequestAsync(requestId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FeedbackCallDetailResponse>> Detail(
        int id, CancellationToken cancellationToken)
        => Ok(await _calls.GetDetailAsync(id, cancellationToken));

    /// <summary>The dashboard for one questionnaire. Always one: averages across two different
    /// sets of questions would be a number about nothing.</summary>
    [HttpGet("dashboard/{surveyId:int}")]
    public async Task<ActionResult<FeedbackDashboardResponse>> Dashboard(
        int surveyId, [FromQuery] Instant? from, [FromQuery] Instant? to, CancellationToken cancellationToken)
        => Ok(await _calls.GetDashboardAsync(surveyId, from, to, cancellationToken));
}
