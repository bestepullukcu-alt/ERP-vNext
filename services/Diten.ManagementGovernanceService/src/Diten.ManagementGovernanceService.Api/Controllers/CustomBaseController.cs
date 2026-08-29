using Diten.ManagementGovernanceService.Application.Features.Dws;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected IActionResult FromResponse<T>(Response<T> response) => response.StatusCode switch
    {
        200 => Ok(response),
        201 => StatusCode(201, response),
        400 => BadRequest(response),
        401 => Unauthorized(response),
        403 => StatusCode(403, response),
        404 => NotFound(response),
        409 => Conflict(response),
        _ => StatusCode(response.StatusCode, response)
    };

    protected IActionResult Result<T>(Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Response<T> response)
    {
        if (!response.Succeeded) return StatusCode(response.StatusCode, response);
        return response.StatusCode switch
        {
            StatusCodes.Status201Created => StatusCode(StatusCodes.Status201Created, response),
            StatusCodes.Status204NoContent => NoContent(),
            _ => StatusCode(response.StatusCode, response)
        };
    }
}
