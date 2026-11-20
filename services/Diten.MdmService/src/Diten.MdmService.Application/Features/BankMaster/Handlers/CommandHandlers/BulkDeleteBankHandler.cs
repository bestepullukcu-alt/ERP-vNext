using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Handlers.CommandHandlers;

/// <summary>
/// POST /api/mdm/banks/bulk-delete — all or none. An id the tenant does not have is 404; one that cannot be deleted (not a
/// never-activated Draft) makes the whole request 409 and nothing is deleted (AC-4). One audit intent per id, each in its
/// bank's own write, all inside one transaction.
/// </summary>
public sealed class BulkDeleteBankHandler : IRequestHandler<BulkDeleteBankCommand, Response<NoContent>>
{
    private readonly IBankRepository _banks;
    private readonly ITenantContext _tenant;
    private readonly IRequestCorrelation _correlation;
    private readonly TimeProvider _clock;

    public BulkDeleteBankHandler(
        IBankRepository banks, ITenantContext tenant, IRequestCorrelation correlation, TimeProvider? clock = null)
    {
        _banks = banks;
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<NoContent>> Handle(BulkDeleteBankCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ids = request.Request.Ids!;
        var banks = await _banks.GetByIdsAsync(ids, cancellationToken);
        if (banks.Count != ids.Count) return BankHandlerSupport.Fail<NoContent>(BankErrorCodes.NotFound, 404);
        if (banks.Any(bank => !bank.CanDelete)) return BankHandlerSupport.Fail<NoContent>(BankErrorCodes.StateInvalid, 409);

        var now = _clock.GetUtcNow();
        var items = new List<BankSoftDelete>(banks.Count);
        foreach (var bank in banks)
        {
            var expected = bank.Version;
            bank.UpdatedBy = request.Actor;
            var intent = BankAuditIntentFactory.Create(
                AuditAggregateType.Bank, _tenant.TenantId, bank.Id, MasterDataAuditOperation.BankDeleted, expected, expected + 1,
                Guid.NewGuid(), _correlation.CorrelationId, request.Actor, now, $"code={bank.Code};status={bank.Status}");
            items.Add(new BankSoftDelete(bank, expected, intent));
        }

        var outcome = await _banks.SoftDeleteManyAsync(items, cancellationToken);
        return outcome switch
        {
            BankStoreOutcome.Written => Response<NoContent>.SuccessWithoutData(204),
            // One of them moved between the read and the write: the whole request is refused, none deleted.
            BankStoreOutcome.Stale => BankHandlerSupport.Fail<NoContent>(BankErrorCodes.VersionConflict, 409),
            _ => BankHandlerSupport.FromOutcome<NoContent>(outcome)
        };
    }
}
