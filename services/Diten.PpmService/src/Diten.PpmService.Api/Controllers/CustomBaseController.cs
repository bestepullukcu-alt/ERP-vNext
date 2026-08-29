using Diten.Shared.Core;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PpmService.Api.Controllers;

[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected IActionResult CreateActionResultInstance<T>(Response<T> response) =>
        response.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(response),
            StatusCodes.Status201Created => Created(string.Empty, response),
            StatusCodes.Status204NoContent => NoContent(),
            StatusCodes.Status400BadRequest => BadRequest(response),
            StatusCodes.Status401Unauthorized => Unauthorized(response),
            StatusCodes.Status403Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
            StatusCodes.Status404NotFound => NotFound(response),
            StatusCodes.Status409Conflict => Conflict(response),
            StatusCodes.Status503ServiceUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, response),
            _ => StatusCode(response.StatusCode, response)
        };
}
