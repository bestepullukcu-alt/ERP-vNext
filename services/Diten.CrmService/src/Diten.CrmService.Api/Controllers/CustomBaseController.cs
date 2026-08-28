using Diten.CrmService.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers;

// Scaffold-only base controller. CRM feature controllers (e.g. Accounts) are added by MOD-0149 implementation.
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
            403 => StatusCode(403, response),
            404 => NotFound(response),
            409 => Conflict(response),
            _ => StatusCode(response.StatusCode, response)
        };
    }
}
