using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Secretary.Api.Controllers;

/// <summary>How many questionnaires a tenant may build. The platform's decision, not theirs.
///
/// ⚠ Deliberately NOT under [RequireModule]. A platform admin has no tenant of their own, so the
/// module check — which reads the caller's tenant — would refuse every request. It is the tenant
/// id in the route that says who this is about, and PlatformAdmin is the only role that can send
/// one.</summary>
[ApiController]
[Authorize(Roles = "PlatformAdmin")]
[Route("api/tenants/{tenantId:int}/feedback-quota")]
public sealed class FeedbackQuotaController : ControllerBase
{
    private readonly FeedbackSettingsService _settings;

    public FeedbackQuotaController(FeedbackSettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<ActionResult<FeedbackSettingsResponse>> Get(int tenantId, CancellationToken cancellationToken)
        => Ok(await _settings.GetForTenantAsync(tenantId, cancellationToken));

    [HttpPut]
    public async Task<ActionResult<FeedbackSettingsResponse>> Update(
        int tenantId, UpdateFeedbackSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.UpdateAsync(tenantId, request, cancellationToken));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
