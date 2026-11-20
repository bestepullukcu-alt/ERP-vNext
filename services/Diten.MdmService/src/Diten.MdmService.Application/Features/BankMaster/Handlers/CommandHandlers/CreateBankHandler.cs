using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Contracts.RecordCodes;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;

/// <summary>
/// POST /api/mdm/banks — a Draft bank whose code comes from the shared record code service (<c>BNK</c>, group prefix:
/// <c>prefixScope = Group</c>, no <c>legalEntityId</c>). Nothing is written unless the code arrives: the port not being bound,
/// any answer that is not a clean 200, a timeout — 503 MDM_BANK_RECORD_CODE_UNAVAILABLE, no record, no intent, no fallback
/// code (AC-60). The document and its audit intent are ONE write (AC-6).
/// </summary>
public sealed class CreateBankHandler : IRequestHandler<CreateBankCommand, Response<BankDetailDto>>
{
    public const string RecordType = "BNK";

    private readonly IBankRepository _banks;
    private readonly ITenantContext _tenant;
    private readonly IRequestCorrelation _correlation;
    private readonly IRecordCodeAllocator? _allocator;
    private readonly TimeProvider _clock;

    public CreateBankHandler(
        IBankRepository banks,
        ITenantContext tenant,
        IRequestCorrelation correlation,
        IRecordCodeAllocator? allocator = null,
        TimeProvider? clock = null)
    {
        _banks = banks;
        _tenant = tenant;
        _correlation = correlation;
        // Fail closed (KARAR D59/1): no allocator bound means no bank — there is no other source of a code.
        _allocator = allocator;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<BankDetailDto>> Handle(CreateBankCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var r = request.Request;
        var name = BankRules.NormalizeText(r.Name)!;
        var country = BankRules.NormalizeCountry(r.CountryCode)!;
        var bic = BankRules.NormalizeBic(r.Bic);
        var nationalCode = BankRules.NormalizeCode(r.NationalBankCode);

        if (_allocator is null)
            return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.RecordCodeUnavailable, 503);

        // A fresh key for each Create attempt, made here (the contract wants 1-200 printable ASCII; MDM derives no hash):
        // a repeat of the same attempt would get the same code, a new attempt a new one.
        var idempotencyKey = "mdm-bank-" + Guid.NewGuid().ToString("N");
        var allocation = await _allocator.AllocateAsync(
            RecordCodePrefixScope.Group, null, RecordType, idempotencyKey, cancellationToken);
        switch (allocation.Outcome)
        {
            case RecordCodeAllocationOutcome.Allocated when IsUsableCode(allocation.Code):
                break;
            case RecordCodeAllocationOutcome.PermissionMissing:
                // The user cannot create banks as far as the code service is concerned: not retried, nothing written.
                return BankHandlerSupport.Fail<BankDetailDto>(allocation.ReasonCode ?? "RECORD_CODE_PERMISSION_MISSING", 403);
            case RecordCodeAllocationOutcome.LegalEntityUnknown:
                return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.LegalEntityUnknown, 422);
            default:
                return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.RecordCodeUnavailable, 503);
        }

        var now = _clock.GetUtcNow();
        var bank = Bank.CreateDraft(
            _tenant.TenantId, allocation.Code!, name, country, bic, nationalCode,
            r.DomesticKeyIncludesBranch ?? false, request.Actor, now);
        bank.AuditIntents.Add(BankAuditIntentFactory.Create(
            AuditAggregateType.Bank, bank.TenantId, bank.Id, MasterDataAuditOperation.BankCreated, -1, 0,
            Guid.NewGuid(), _correlation.CorrelationId, request.Actor, now,
            $"code={bank.Code};status={bank.Status};country={bank.CountryCode};bic={(bank.Bic is null ? "none" : "set")}"));

        var outcome = await _banks.CreateAsync(bank, cancellationToken);
        return outcome == BankStoreOutcome.Written
            ? Response<BankDetailDto>.Success(BankMappings.ToDetail(bank), 201)
            : BankHandlerSupport.FromOutcome<BankDetailDto>(outcome);
    }

    // A code from the service is displayed and stored as is, so it must be a plain printable value of a sane length.
    private static bool IsUsableCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && code.Length <= 64 && code.All(c => c is > ' ' and <= '~');
}
