using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;

/// <summary>DELETE /api/mdm/bank-branches/{id} — soft delete of a Draft branch that was never activated, with its intent.</summary>
public sealed class DeleteBankBranchHandler : IRequestHandler<DeleteBankBranchCommand, Response<NoContent>>
{
    private readonly IBankBranchRepository _branches;
    private readonly ITenantContext _tenant;
    private readonly IRequestCorrelation _correlation;
    private readonly TimeProvider _clock;

    public DeleteBankBranchHandler(
        IBankBranchRepository branches, ITenantContext tenant, IRequestCorrelation correlation, TimeProvider? clock = null)
    {
        _branches = branches;
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<NoContent>> Handle(DeleteBankBranchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var branch = await _branches.GetByIdAsync(request.BranchId, cancellationToken);
        if (branch is null) return BankHandlerSupport.Fail<NoContent>(BankErrorCodes.BranchNotFound, 404);
        if (!branch.CanDelete) return BankHandlerSupport.Fail<NoContent>(BankErrorCodes.StateInvalid, 409);

        var expected = branch.Version;
        var now = _clock.GetUtcNow();
        var intent = BankAuditIntentFactory.Create(
            AuditAggregateType.BankBranch, _tenant.TenantId, branch.Id, MasterDataAuditOperation.BankBranchDeleted, expected,
            expected + 1, Guid.NewGuid(), _correlation.CorrelationId, request.Actor, now,
            $"branch={branch.BranchCode};status={branch.Status}");
        branch.UpdatedBy = request.Actor;

        var outcome = await _branches.SoftDeleteAsync(branch, expected, intent, cancellationToken);
        if (outcome == BankStoreOutcome.Written) return Response<NoContent>.SuccessWithoutData(204);
        if (outcome != BankStoreOutcome.Stale) return BankHandlerSupport.FromOutcome<NoContent>(outcome);
        var stored = await _branches.GetByIdAsync(request.BranchId, cancellationToken);
        return BankHandlerSupport.FromStale<NoContent>(stored?.Version, expected, BankErrorCodes.BranchNotFound);
    }
}
