using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;

/// <summary>
/// PUT /api/mdm/banks/{id} with <c>If-Match</c> = the version. A Draft edits every field; an Active bank only its name (any
/// other field that differs is 409 MDM_BANK_STATE_INVALID); an Inactive one nothing (the Durumlar table). A write that
/// changes a field the domestic account key is built from moves <c>DomesticKeyFenceVersion</c> in the same write (gap 14/2).
/// The conditional write carries the version (AC-9): a stale one never overwrites.
/// </summary>
public sealed class UpdateBankHandler : IRequestHandler<UpdateBankCommand, Response<BankDetailDto>>
{
    private readonly IBankRepository _banks;
    private readonly ITenantContext _tenant;
    private readonly IRequestCorrelation _correlation;
    private readonly TimeProvider _clock;

    public UpdateBankHandler(
        IBankRepository banks, ITenantContext tenant, IRequestCorrelation correlation, TimeProvider? clock = null)
    {
        _banks = banks;
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<BankDetailDto>> Handle(UpdateBankCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bank = await _banks.GetByIdAsync(request.BankId, cancellationToken);
        if (bank is null) return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.NotFound, 404);
        if (bank.Version != request.ExpectedVersion)
            return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.VersionConflict, 409);
        if (!bank.CanUpdate) return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.StateInvalid, 409);

        var r = request.Request;
        var name = BankRules.NormalizeText(r.Name)!;
        var country = BankRules.NormalizeCountry(r.CountryCode)!;
        var bic = BankRules.NormalizeBic(r.Bic);
        var nationalCode = BankRules.NormalizeCode(r.NationalBankCode);
        var includesBranch = r.DomesticKeyIncludesBranch ?? bank.DomesticKeyIncludesBranch;

        if (bank.UpdateIsLimited
            && (country != bank.CountryCode || bic != bank.Bic || nationalCode != bank.NationalBankCode
                || includesBranch != bank.DomesticKeyIncludesBranch))
            return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.StateInvalid, 409);

        var changed = new List<string>();
        if (name != bank.Name) changed.Add(nameof(bank.Name));
        if (country != bank.CountryCode) changed.Add(nameof(bank.CountryCode));
        if (bic != bank.Bic) changed.Add(nameof(bank.Bic));
        if (nationalCode != bank.NationalBankCode) changed.Add(nameof(bank.NationalBankCode));
        if (includesBranch != bank.DomesticKeyIncludesBranch) changed.Add(nameof(bank.DomesticKeyIncludesBranch));
        // Nothing to change: no write, no new version, no intent — the caller already holds the current state.
        if (changed.Count == 0) return Response<BankDetailDto>.Success(BankMappings.ToDetail(bank));

        var movesKey = bank.ChangesDomesticKey(country, nationalCode, includesBranch);
        var now = _clock.GetUtcNow();
        bank.Name = name;
        bank.CountryCode = country;
        bank.Bic = bic;
        bank.NationalBankCode = nationalCode;
        bank.DomesticKeyIncludesBranch = includesBranch;
        if (movesKey) bank.DomesticKeyFenceVersion++;
        bank.UpdatedBy = request.Actor;
        bank.UpdatedAt = now;

        var expected = request.ExpectedVersion;
        var intent = BankAuditIntentFactory.Create(
            AuditAggregateType.Bank, _tenant.TenantId, bank.Id, MasterDataAuditOperation.BankUpdated, expected, expected + 1,
            Guid.NewGuid(), _correlation.CorrelationId, request.Actor, now, "changed=" + string.Join(',', changed));

        var outcome = await _banks.UpdateAsync(bank, expected, intent, cancellationToken);
        if (outcome == BankStoreOutcome.Written)
        {
            bank.Version = expected + 1;
            return Response<BankDetailDto>.Success(BankMappings.ToDetail(bank));
        }

        if (outcome != BankStoreOutcome.Stale) return BankHandlerSupport.FromOutcome<BankDetailDto>(outcome);
        var stored = await _banks.GetByIdAsync(request.BankId, cancellationToken);
        return BankHandlerSupport.FromStale<BankDetailDto>(stored?.Version, expected, BankErrorCodes.NotFound);
    }
}
