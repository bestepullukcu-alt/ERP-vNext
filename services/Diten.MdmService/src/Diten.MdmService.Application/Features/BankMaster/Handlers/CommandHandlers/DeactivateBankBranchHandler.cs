using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;

/// <summary>POST /api/mdm/bank-branches/{id}/deactivate with <c>If-Match</c> and a reason code: Active → Inactive.</summary>
public sealed class DeactivateBankBranchHandler : IRequestHandler<DeactivateBankBranchCommand, Response<BankBranchDto>>
{
    private readonly IBankBranchRepository _branches;
    private readonly ITenantContext _tenant;
    private readonly IRequestCorrelation _correlation;
    private readonly TimeProvider _clock;

    public DeactivateBankBranchHandler(
        IBankBranchRepository branches, ITenantContext tenant, IRequestCorrelation correlation, TimeProvider? clock = null)
    {
        _branches = branches;
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<BankBranchDto>> Handle(DeactivateBankBranchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var branch = await _branches.GetByIdAsync(request.BranchId, cancellationToken);
        if (branch is null) return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.BranchNotFound, 404);
        if (branch.Version != request.ExpectedVersion)
            return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.VersionConflict, 409);
        if (!branch.CanDeactivate) return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.StateInvalid, 409);

        var reason = BankRules.NormalizeText(request.Request.ReasonCode)!;
        var before = branch.Status;
        var now = _clock.GetUtcNow();
        branch.Deactivate(request.Actor, reason, now);
        var expected = request.ExpectedVersion;
        var intent = BankAuditIntentFactory.Create(
            AuditAggregateType.BankBranch, _tenant.TenantId, branch.Id, MasterDataAuditOperation.BankBranchDeactivated, expected,
            expected + 1, Guid.NewGuid(), _correlation.CorrelationId, request.Actor, now,
            $"status={before}>{branch.Status};reason={reason}");

        var outcome = await _branches.UpdateAsync(branch, expected, intent, cancellationToken);
        if (outcome == BankStoreOutcome.Written)
        {
            branch.Version = expected + 1;
            return Response<BankBranchDto>.Success(BankMappings.ToDto(branch));
        }

        if (outcome != BankStoreOutcome.Stale) return BankHandlerSupport.FromOutcome<BankBranchDto>(outcome);
        var stored = await _branches.GetByIdAsync(request.BranchId, cancellationToken);
        return BankHandlerSupport.FromStale<BankBranchDto>(stored?.Version, expected, BankErrorCodes.BranchNotFound);
    }
}
