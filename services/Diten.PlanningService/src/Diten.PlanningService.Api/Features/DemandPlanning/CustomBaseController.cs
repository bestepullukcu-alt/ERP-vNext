using Diten.PlanningService.Application.Features.DemandPlanning;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected IActionResult CreateActionResultInstance<T>(Response<T> response) =>
        response.StatusCode switch
        {
            200 => Ok(response),
            201 => Created(string.Empty, response),
            204 => NoContent(),
            400 => BadRequest(response),
            403 => StatusCode(403, response),
            404 => NotFound(response),
            409 => Conflict(response),
            _ => StatusCode(response.StatusCode, response)
        };
}