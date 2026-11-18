using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;

/// <summary>
/// PUT /api/mdm/bank-branches/{id} with <c>If-Match</c>. A Draft edits code, name, city and BIC; an Active branch only name and
/// city (code or BIC that differ is 409 MDM_BANK_STATE_INVALID); an Inactive one nothing. A body naming another bank is 400
/// MDM_BANK_BRANCH_BANK_MISMATCH. A changed branch code moves the branch's key fence in the same write (gap 14/2).
/// </summary>
public sealed class UpdateBankBranchHandler : IRequestHandler<UpdateBankBranchCommand, Response<BankBranchDto>>
{
    private readonly IBankRepository _banks;
    private readonly IBankBranchRepository _branches;
    private readonly ITenantContext _tenant;
    private readonly IRequestCorrelation _correlation;
    private readonly TimeProvider _clock;

    public UpdateBankBranchHandler(
        IBankRepository banks,
        IBankBranchRepository branches,
        ITenantContext tenant,
        IRequestCorrelation correlation,
        TimeProvider? clock = null)
    {
        _banks = banks;
        _branches = branches;
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<BankBranchDto>> Handle(UpdateBankBranchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var r = request.Request;
        var branch = await _branches.GetByIdAsync(request.BranchId, cancellationToken);
        if (branch is null) return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.BranchNotFound, 404);
        if (r.BankId is { } named && named != branch.BankId)
            return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.BranchBankMismatch, 400);
        if (branch.Version != request.ExpectedVersion)
            return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.VersionConflict, 409);
        if (!branch.CanUpdate) return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.StateInvalid, 409);

        var bank = await _banks.GetByIdAsync(branch.BankId, cancellationToken);
        if (bank is null) return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.BranchNotFound, 404);
        var rule = BankBranchRules.Judge(bank, r);
        if (rule is not null) return BankHandlerSupport.Fail<BankBranchDto>(rule, 400);

        var code = BankRules.NormalizeCode(r.BranchCode)!;
        var name = BankRules.NormalizeText(r.Name)!;
        var city = BankRules.NormalizeText(r.City);
        var bic = BankRules.NormalizeBic(r.Bic);
        if (branch.UpdateIsLimited && (code != branch.BranchCode || bic != branch.Bic))
            return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.StateInvalid, 409);

        var changed = new List<string>();
        if (code != branch.BranchCode) changed.Add(nameof(branch.BranchCode));
        if (name != branch.Name) changed.Add(nameof(branch.Name));
        if (city != branch.City) changed.Add(nameof(branch.City));
        if (bic != branch.Bic) changed.Add(nameof(branch.Bic));
        if (changed.Count == 0) return Response<BankBranchDto>.Success(BankMappings.ToDto(branch));

        var movesKey = branch.ChangesDomesticKey(code);
        var now = _clock.GetUtcNow();
        branch.BranchCode = code;
        branch.Name = name;
        branch.City = city;
        branch.Bic = bic;
        if (movesKey) branch.DomesticKeyFenceVersion++;
        branch.UpdatedBy = request.Actor;
        branch.UpdatedAt = now;

        var expected = request.ExpectedVersion;
        var intent = BankAuditIntentFactory.Create(
            AuditAggregateType.BankBranch, _tenant.TenantId, branch.Id, MasterDataAuditOperation.BankBranchUpdated, expected,
            expected + 1, Guid.NewGuid(), _correlation.CorrelationId, request.Actor, now, "changed=" + string.Join(',', changed));

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
