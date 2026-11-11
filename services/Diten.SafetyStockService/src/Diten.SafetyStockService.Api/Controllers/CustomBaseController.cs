using Diten.SafetyStockService.Application;
using Microsoft.AspNetCore.Mvc;

namespace Diten.SafetyStockService.Api.Controllers;

[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected IActionResult ReadResponse(PolicyReadResult result) => result.Status switch
    {
        ReadStatus.Found => Ok(new { data = result.Data }),
        ReadStatus.NotFound => NotFound(),
        _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "The authorized read source is unavailable." })
    };
}
