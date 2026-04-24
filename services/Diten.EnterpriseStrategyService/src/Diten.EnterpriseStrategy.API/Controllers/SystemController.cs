using System.Threading.Tasks;
using Asp.Versioning;
using Diten.Application.Features.System.Queries.Ping;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiVersion("1.0")]
public class SystemController : BaseApiController
{
    [HttpGet("ping")]
    public async Task<IActionResult> Ping()
    {
        return CreateActionResultInstance(await Mediator.Send(new PingQuery()));
    }
}
