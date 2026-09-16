using Microsoft.AspNetCore.Mvc;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Api.Middleware;
namespace Diten.SupplyChainService.Api.Controllers;
[ApiController]
public abstract class CustomBaseController : ControllerBase
{
    protected IActionResult Wire<T>(Response<T> response)
    {
        var context = HttpContext.RequestServices.GetRequiredService<RequestContext>();
        return StatusCode(response.StatusCode, response.ErrorCode is null ? response.Data : ContractError.Create(response.ErrorCode, response.Message ?? response.ErrorCode, context.CorrelationId));
    }
}
