using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HcmService.Api.Controllers.Hcm;

[Authorize]
[ApiController]
[Route("api/v1/hcm/employees/smoke-fixtures")]
[HasPermission("mod0251.employee.view")]
[HasPermission("mod0251.employee.create_draft")]
public sealed class EmployeeSmokeFixturesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly IHostEnvironment _environment;

    public EmployeeSmokeFixturesController(IMediator mediator, IHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    [HttpPost("minimal")]
    public async Task<IActionResult> EnsureMinimal(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new EnsureEmployeeSmokeFixtureCommand(IsLocalFixtureEnabled(_environment)),
            cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("minimal")]
    public async Task<IActionResult> CleanupMinimal(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new CleanupEmployeeSmokeFixtureCommand(IsLocalFixtureEnabled(_environment)),
            cancellationToken);
        return CreateActionResultInstance(response);
    }

    private static bool IsLocalFixtureEnabled(IHostEnvironment environment)
        => environment.IsDevelopment()
           || environment.IsEnvironment("Testing")
           || environment.IsEnvironment("Local");
}
