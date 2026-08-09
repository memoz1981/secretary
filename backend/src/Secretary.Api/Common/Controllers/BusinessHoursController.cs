using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;

namespace Secretary.Api.Controllers;

/// <summary>Opening hours, on Administration rather than in a module: one business, one set of
/// hours, however many modules it holds. Appointments will not offer a slot outside them and
/// Orders will not promise a delivery outside them, so no [RequireModule] here.</summary>
[ApiController]
[Authorize(Roles = "Owner,Staff")]
[Route("api/business-hours")]
public sealed class BusinessHoursController : ControllerBase
{
    private readonly BusinessHoursService _businessHours;

    public BusinessHoursController(BusinessHoursService businessHours) => _businessHours = businessHours;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BusinessHoursDay>>> Get(CancellationToken cancellationToken)
        => Ok(await _businessHours.GetWeekForEditingAsync(cancellationToken));

    [HttpPut]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<IReadOnlyList<BusinessHoursDay>>> Update(
        UpdateBusinessHoursRequest request, CancellationToken cancellationToken)
    {
        // Both times or neither: a day with an opening time and no closing time is not a shorter
        // day, it is a day nothing can decide about.
        foreach (var day in request.Days)
        {
            if (day.OpensAt is null != day.ClosesAt is null)
            {
                return BadRequest($"{day.DayOfWeek} needs both an opening and a closing time, or neither.");
            }

            if (day.OpensAt is not null && day.ClosesAt <= day.OpensAt)
            {
                return BadRequest($"{day.DayOfWeek} closes before it opens.");
            }
        }

        return Ok(await _businessHours.UpdateWeekAsync(request, cancellationToken));
    }
}
