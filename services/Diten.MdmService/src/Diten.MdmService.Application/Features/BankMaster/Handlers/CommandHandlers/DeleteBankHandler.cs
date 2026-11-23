using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;

/// <summary>
/// DELETE /api/mdm/banks/{id} — soft delete (IsDeleted + DeletedAt, UTC) of a Draft that was never activated; anything else is
/// 409 MDM_BANK_STATE_INVALID. The code stays taken (the unique code index has no partial filter).
/// </summary>
public sealed class DeleteBankHandler : IRequestHandler<DeleteBankCommand, Response<NoContent>>
{
    private readonly IBankRepository _banks;
    private readonly ITenantContext _tenant;
    private readonly IRequestCorrelation _correlation;
    private readonly TimeProvider _clock;

    public DeleteBankHandler(
        IBankRepository banks, ITenantContext tenant, IRequestCorrelation correlation, TimeProvider? clock = null)
    {
        _banks = banks;
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<NoContent>> Handle(DeleteBankCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bank = await _banks.GetByIdAsync(request.BankId, cancellationToken);
        if (bank is null) return BankHandlerSupport.Fail<NoContent>(BankErrorCodes.NotFound, 404);
        if (!bank.CanDelete) return BankHandlerSupport.Fail<NoContent>(BankErrorCodes.StateInvalid, 409);

        var expected = bank.Version;
        var now = _clock.GetUtcNow();
        var intent = BankAuditIntentFactory.Create(
            AuditAggregateType.Bank, _tenant.TenantId, bank.Id, MasterDataAuditOperation.BankDeleted, expected, expected + 1,
            Guid.NewGuid(), _correlation.CorrelationId, request.Actor, now, $"code={bank.Code};status={bank.Status}");
        bank.UpdatedBy = request.Actor;

        var outcome = await _banks.SoftDeleteAsync(bank, expected, intent, cancellationToken);
        if (outcome == BankStoreOutcome.Written) return Response<NoContent>.SuccessWithoutData(204);
        if (outcome != BankStoreOutcome.Stale) return BankHandlerSupport.FromOutcome<NoContent>(outcome);
        var stored = await _banks.GetByIdAsync(request.BankId, cancellationToken);
        return BankHandlerSupport.FromStale<NoContent>(stored?.Version, expected, BankErrorCodes.NotFound);
    }
}
