using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[ApiController]
public abstract class CustomBaseController:ControllerBase
{
    protected IActionResult Result<T>(Response<T> response)
    {
        if(!response.Succeeded)return StatusCode(response.StatusCode,response);
        return response.StatusCode switch
        {
            StatusCodes.Status201Created=>StatusCode(StatusCodes.Status201Created,response),
            StatusCodes.Status204NoContent=>NoContent(),
            _=>StatusCode(response.StatusCode,response)
        };
    }
}
