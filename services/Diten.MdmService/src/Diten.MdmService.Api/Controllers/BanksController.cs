using System.Security.Claims;
using Diten.MdmService.Application.Features.BankMaster;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Application.Features.BankMaster.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// MOD-0289 DL-1 — the bank master. Routes are fixed lower case (no PATCH: the gateway's MDM routes carry GET, POST, PUT, DELETE).
/// Every action holds its <c>[HasPermission]</c>; the controller only sends a request and answers its <c>Response</c>. The
/// actor is the token's <c>sub</c> (a Guid — audit identity only, never authorization); a write needs <c>If-Match</c> = version.
/// </summary>
[Authorize]
[ApiController]
[Route("api/mdm/banks")]
public sealed class BanksController : CustomBaseController
{
    private readonly IMediator _mediator;

    public BanksController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("mdm.banks.read")]
    public async Task<IActionResult> GetList([FromQuery] GetBankListQuery query, CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(query, cancellationToken));

    [HttpGet("{bankId:guid}")]
    [HasPermission("mdm.banks.read")]
    public async Task<IActionResult> GetById(Guid bankId, CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(new GetBankByIdQuery(bankId), cancellationToken));

    [HttpPost]
    [HasPermission("mdm.banks.create")]
    public async Task<IActionResult> Create([FromBody] BankWriteRequest request, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        return CreateActionResultInstance(await _mediator.Send(new CreateBankCommand(request, actor), cancellationToken));
    }

    [HttpPut("{bankId:guid}")]
    [HasPermission("mdm.banks.update")]
    public async Task<IActionResult> Update(
        Guid bankId, [FromBody] BankWriteRequest request, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        if (!BankRequestContext.TryReadIfMatch(Request, out var version)) return BankRequestContext.IfMatchRefused();
        return CreateActionResultInstance(
            await _mediator.Send(new UpdateBankCommand(bankId, request, version, actor), cancellationToken));
    }

    [HttpPost("{bankId:guid}/activate")]
    [HasPermission("mdm.banks.activate")]
    public async Task<IActionResult> Activate(Guid bankId, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        if (!BankRequestContext.TryReadIfMatch(Request, out var version)) return BankRequestContext.IfMatchRefused();
        return CreateActionResultInstance(
            await _mediator.Send(new ActivateBankCommand(bankId, version, actor), cancellationToken));
    }

    [HttpPost("{bankId:guid}/deactivate")]
    [HasPermission("mdm.banks.deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid bankId, [FromBody] DeactivateRequest request, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        if (!BankRequestContext.TryReadIfMatch(Request, out var version)) return BankRequestContext.IfMatchRefused();
        return CreateActionResultInstance(
            await _mediator.Send(new DeactivateBankCommand(bankId, request, version, actor), cancellationToken));
    }

    [HttpDelete("{bankId:guid}")]
    [HasPermission("mdm.banks.delete")]
    public async Task<IActionResult> Delete(Guid bankId, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        return CreateActionResultInstance(await _mediator.Send(new DeleteBankCommand(bankId, actor), cancellationToken));
    }

    [HttpPost("bulk-delete")]
    [HasPermission("mdm.banks.bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteBanksRequest request, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        return CreateActionResultInstance(
            await _mediator.Send(new BulkDeleteBankCommand(request, actor), cancellationToken));
    }
}

/// <summary>What the two bank controllers read from the request: the actor (token <c>sub</c>) and <c>If-Match</c>.</summary>
internal static class BankRequestContext
{
    public static bool TryReadActor(ClaimsPrincipal user, out Guid actor)
    {
        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out actor) && actor != Guid.Empty;
    }

    /// <summary><c>If-Match: "3"</c>, <c>W/"3"</c> or <c>3</c> — the version; anything else (or nothing) is not a version.</summary>
    public static bool TryReadIfMatch(HttpRequest request, out int version)
    {
        version = 0;
        if (!request.Headers.TryGetValue("If-Match", out var values) || values.Count != 1) return false;
        var text = values[0]!.Trim();
        if (text.StartsWith("W/", StringComparison.Ordinal)) text = text[2..];
        text = text.Trim('"');
        return int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out version);
    }

    public static IActionResult ActorRefused() =>
        new ObjectResult(Response<NoContent>.Fail(BankErrorCodes.ActorInvalid, 403)) { StatusCode = 403 };

    public static IActionResult IfMatchRefused() =>
        new BadRequestObjectResult(Response<NoContent>.Fail(BankErrorCodes.IfMatchRequired, 400));
}
