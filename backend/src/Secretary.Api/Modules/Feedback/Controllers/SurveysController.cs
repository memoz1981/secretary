using Secretary.Api.Auth;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secretary.Domain.Enums;

namespace Secretary.Api.Controllers;

/// <summary>The questionnaires and the questions in them. Owner writes, Staff reads — same split
/// as Services and Products, because this is the same kind of thing: the shape of what the agent
/// will say.</summary>
[ApiController]
[RequireModule(Module.Feedback)]
[Authorize(Roles = "Owner,Staff")]
[Route("api/feedback/surveys")]
public sealed class SurveysController : ControllerBase
{
    private readonly SurveyService _surveys;
    private readonly FeedbackSettingsService _settings;

    public SurveysController(SurveyService surveys, FeedbackSettingsService settings)
    {
        _surveys = surveys;
        _settings = settings;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SurveyResponse>>> List(CancellationToken cancellationToken)
        => Ok(await _surveys.ListAsync(cancellationToken));

    /// <summary>The quota and how much of it is used, so the page can say "3 of 3" rather than
    /// only refusing the fourth.</summary>
    [HttpGet("quota")]
    public async Task<ActionResult<FeedbackSettingsResponse>> Quota(CancellationToken cancellationToken)
        => Ok(await _settings.GetForCurrentTenantAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SurveyDetailResponse>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _surveys.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<SurveyResponse>> Create(
        SaveSurveyRequest request, CancellationToken cancellationToken)
        => Ok(await _surveys.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<SurveyResponse>> Rename(
        int id, SaveSurveyRequest request, CancellationToken cancellationToken)
        => Ok(await _surveys.RenameAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        await _surveys.RemoveAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/questions")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<SurveyQuestionResponse>> AddQuestion(
        int id, SaveQuestionRequest request, CancellationToken cancellationToken)
        => Ok(await _surveys.AddQuestionAsync(id, request, cancellationToken));

    [HttpPut("questions/{questionId:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<SurveyQuestionResponse>> UpdateQuestion(
        int questionId, SaveQuestionRequest request, CancellationToken cancellationToken)
        => Ok(await _surveys.UpdateQuestionAsync(questionId, request, cancellationToken));

    [HttpDelete("questions/{questionId:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> RemoveQuestion(int questionId, CancellationToken cancellationToken)
    {
        await _surveys.RemoveQuestionAsync(questionId, cancellationToken);
        return NoContent();
    }

    /// <summary>The whole running order at once. Reordering is a drag on the page, and one
    /// request per moved question would leave the list half-reordered if any of them failed.</summary>
    [HttpPut("{id:int}/questions/order")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<SurveyDetailResponse>> Reorder(
        int id, ReorderQuestionsRequest request, CancellationToken cancellationToken)
        => Ok(await _surveys.ReorderAsync(id, request, cancellationToken));
}
