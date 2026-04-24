using Diten.Platform.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Common;

[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected ActionResult<Response<T>> OkResponse<T>(T data, string message = "OK")
    {
        return Ok(Response<T>.Ok(data, message));
    }

    protected ActionResult<Response<T>> CreatedResponse<T>(T data, string message = "Created")
    {
        return StatusCode(StatusCodes.Status201Created, Response<T>.Ok(data, message));
    }
}
