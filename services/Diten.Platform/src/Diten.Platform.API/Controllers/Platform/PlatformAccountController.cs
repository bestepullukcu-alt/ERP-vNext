using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Features.PlatformAccount;
using Diten.Platform.Application.Features.PlatformAccount.Commands;
using Diten.Platform.Application.Features.PlatformAccount.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/account")]
[Authorize(Policy = "PlatformActor")]
public sealed class PlatformAccountController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PlatformAccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPlatformAccountProfileQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdatePlatformAccountSettingsRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdatePlatformAccountSettingsCommand(request), ct);
        return CreateActionResultInstance(response);
    }
}
