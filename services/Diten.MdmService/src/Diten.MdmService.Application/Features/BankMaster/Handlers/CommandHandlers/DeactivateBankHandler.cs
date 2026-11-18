using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;

/// <summary>
/// POST /api/mdm/banks/{id}/deactivate with <c>If-Match</c> and a reason code. Active → Inactive. The bank's branches and
/// accounts are NOT changed (status is not derived): their documents keep their versions (AC-5).
/// </summary>
public sealed class DeactivateBankHandler : IRequestHandler<DeactivateBankCommand, Response<BankDetailDto>>
{
    private readonly IBankRepository _banks;
    private readonly ITenantContext _tenant;
    private readonly IRequestCorrelation _correlation;
    private readonly TimeProvider _clock;

    public DeactivateBankHandler(
        IBankRepository banks, ITenantContext tenant, IRequestCorrelation correlation, TimeProvider? clock = null)
    {
        _banks = banks;
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<BankDetailDto>> Handle(DeactivateBankCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bank = await _banks.GetByIdAsync(request.BankId, cancellationToken);
        if (bank is null) return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.NotFound, 404);
        if (bank.Version != request.ExpectedVersion)
            return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.VersionConflict, 409);
        if (!bank.CanDeactivate) return BankHandlerSupport.Fail<BankDetailDto>(BankErrorCodes.StateInvalid, 409);

        var reason = BankRules.NormalizeText(request.Request.ReasonCode)!;
        var before = bank.Status;
        var now = _clock.GetUtcNow();
        bank.Deactivate(request.Actor, reason, now);
        var expected = request.ExpectedVersion;
        var intent = BankAuditIntentFactory.Create(
            AuditAggregateType.Bank, _tenant.TenantId, bank.Id, MasterDataAuditOperation.BankDeactivated, expected, expected + 1,
            Guid.NewGuid(), _correlation.CorrelationId, request.Actor, now, $"status={before}>{bank.Status};reason={reason}");

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
