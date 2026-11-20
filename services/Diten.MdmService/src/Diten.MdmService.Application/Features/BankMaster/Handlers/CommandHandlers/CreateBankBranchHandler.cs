using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;

/// <summary>
/// POST /api/mdm/banks/{bankId}/branches — a Draft branch of an existing, not deleted bank of the tenant. The branch code is
/// its natural key inside the bank (no code service). A branch of an Inactive bank is 422 MDM_BANK_INACTIVE; a body that names
/// another bank is 400 MDM_BANK_BRANCH_BANK_MISMATCH; the branch's country equals the bank's and its BIC's first 8 characters
/// equal the bank's BIC.
/// </summary>
public sealed class CreateBankBranchHandler : IRequestHandler<CreateBankBranchCommand, Response<BankBranchDto>>
{
    private readonly IBankRepository _banks;
    private readonly IBankBranchRepository _branches;
    private readonly ITenantContext _tenant;
    private readonly IRequestCorrelation _correlation;
    private readonly TimeProvider _clock;

    public CreateBankBranchHandler(
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

    public async Task<Response<BankBranchDto>> Handle(CreateBankBranchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var r = request.Request;
        if (r.BankId is { } named && named != request.BankId)
            return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.BranchBankMismatch, 400);

        var bank = await _banks.GetByIdAsync(request.BankId, cancellationToken);
        if (bank is null) return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.NotFound, 404);
        if (bank.Status == BankLifecycleStatus.Inactive) return BankHandlerSupport.Fail<BankBranchDto>(BankErrorCodes.Inactive, 422);

        var rule = BankBranchRules.Judge(bank, r);
        if (rule is not null) return BankHandlerSupport.Fail<BankBranchDto>(rule, 400);

        var now = _clock.GetUtcNow();
        var branch = BankBranch.CreateDraft(
            _tenant.TenantId, bank.Id, BankRules.NormalizeCode(r.BranchCode)!, BankRules.NormalizeText(r.Name)!,
            bank.CountryCode, BankRules.NormalizeText(r.City), BankRules.NormalizeBic(r.Bic), request.Actor, now);
        branch.AuditIntents.Add(BankAuditIntentFactory.Create(
            AuditAggregateType.BankBranch, branch.TenantId, branch.Id, MasterDataAuditOperation.BankBranchCreated, -1, 0,
            Guid.NewGuid(), _correlation.CorrelationId, request.Actor, now,
            $"bank={branch.BankId:D};branch={branch.BranchCode};status={branch.Status}"));

        var outcome = await _branches.CreateAsync(branch, cancellationToken);
        return outcome == BankStoreOutcome.Written
            ? Response<BankBranchDto>.Success(BankMappings.ToDto(branch), 201)
            : BankHandlerSupport.FromOutcome<BankBranchDto>(outcome);
    }
}
