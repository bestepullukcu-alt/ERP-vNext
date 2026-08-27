using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[ApiController]
public abstract class CustomBaseController:ControllerBase
{
    protected IActionResult Result(Response<object> response,bool create)
    {
        if(!response.Succeeded)return StatusCode(response.StatusCode,response);
        if(create)return StatusCode(StatusCodes.Status201Created,response);
        return NoContent();
    }
}
