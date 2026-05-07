using Diten.Platform.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Common;

[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected IActionResult CreateActionResultInstance<T>(Response<T> response)
    {
        return response.StatusCode switch
        {
            200 => Ok(response),
            201 => Created(string.Empty, response),
            204 => NoContent(),
            400 => BadRequest(response),
            401 => Unauthorized(response),
            403 => StatusCode(StatusCodes.Status403Forbidden, response),
            404 => NotFound(response),
            409 => Conflict(response),
            422 => UnprocessableEntity(response),
            _ => StatusCode(response.StatusCode, response)
        };
    }
}
