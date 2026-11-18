using Diten.MdmService.Application.Features.BankMaster;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Application.Features.BankMaster.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// MOD-0289 DL-1 — the branches of a bank: the collection lives under the bank (<c>/api/mdm/banks/{bankId}/branches</c>), a
/// single branch under <c>/api/mdm/bank-branches/{id}</c>. The same <c>mdm.banks.*</c> keys as the bank (one family for the
/// bank and its branches). Every action holds its <c>[HasPermission]</c>.
/// </summary>
[Authorize]
[ApiController]
public sealed class BankBranchesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public BankBranchesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/mdm/banks/{bankId:guid}/branches")]
    [HasPermission("mdm.banks.read")]
    public async Task<IActionResult> GetList(
        Guid bankId, [FromQuery] GetBankBranchListQuery query, CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(query with { BankId = bankId }, cancellationToken));

    [HttpPost("api/mdm/banks/{bankId:guid}/branches")]
    [HasPermission("mdm.banks.create")]
    public async Task<IActionResult> Create(
        Guid bankId, [FromBody] BankBranchWriteRequest request, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        return CreateActionResultInstance(
            await _mediator.Send(new CreateBankBranchCommand(bankId, request, actor), cancellationToken));
    }

    [HttpGet("api/mdm/bank-branches/{branchId:guid}")]
    [HasPermission("mdm.banks.read")]
    public async Task<IActionResult> GetById(Guid branchId, CancellationToken cancellationToken) =>
        CreateActionResultInstance(await _mediator.Send(new GetBankBranchByIdQuery(branchId), cancellationToken));

    [HttpPut("api/mdm/bank-branches/{branchId:guid}")]
    [HasPermission("mdm.banks.update")]
    public async Task<IActionResult> Update(
        Guid branchId, [FromBody] BankBranchWriteRequest request, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        if (!BankRequestContext.TryReadIfMatch(Request, out var version)) return BankRequestContext.IfMatchRefused();
        return CreateActionResultInstance(
            await _mediator.Send(new UpdateBankBranchCommand(branchId, request, version, actor), cancellationToken));
    }

    [HttpPost("api/mdm/bank-branches/{branchId:guid}/activate")]
    [HasPermission("mdm.banks.activate")]
    public async Task<IActionResult> Activate(Guid branchId, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        if (!BankRequestContext.TryReadIfMatch(Request, out var version)) return BankRequestContext.IfMatchRefused();
        return CreateActionResultInstance(
            await _mediator.Send(new ActivateBankBranchCommand(branchId, version, actor), cancellationToken));
    }

    [HttpPost("api/mdm/bank-branches/{branchId:guid}/deactivate")]
    [HasPermission("mdm.banks.deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid branchId, [FromBody] DeactivateRequest request, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        if (!BankRequestContext.TryReadIfMatch(Request, out var version)) return BankRequestContext.IfMatchRefused();
        return CreateActionResultInstance(
            await _mediator.Send(new DeactivateBankBranchCommand(branchId, request, version, actor), cancellationToken));
    }

    [HttpDelete("api/mdm/bank-branches/{branchId:guid}")]
    [HasPermission("mdm.banks.delete")]
    public async Task<IActionResult> Delete(Guid branchId, CancellationToken cancellationToken)
    {
        if (!BankRequestContext.TryReadActor(User, out var actor)) return BankRequestContext.ActorRefused();
        return CreateActionResultInstance(await _mediator.Send(new DeleteBankBranchCommand(branchId, actor), cancellationToken));
    }
}
