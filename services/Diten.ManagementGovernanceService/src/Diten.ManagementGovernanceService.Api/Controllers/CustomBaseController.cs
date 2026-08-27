using Diten.ManagementGovernanceService.Application.Features.Dws;
using ProcessModelingResponse = Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Response<object>;
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

    protected IActionResult Result(ProcessModelingResponse response, bool create)
    {
        if (!response.Succeeded) return StatusCode(response.StatusCode, response);
        return create ? StatusCode(StatusCodes.Status201Created, response) : NoContent();
    }
}
