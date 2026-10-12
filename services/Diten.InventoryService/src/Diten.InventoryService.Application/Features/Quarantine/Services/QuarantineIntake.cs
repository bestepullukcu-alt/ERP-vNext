using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// Intake (pack §8 "Where holds come from", §16 Q1): an INBOUND ledger movement into QUARANTINE / QUALITY_INSPECTION that
/// MOD-0175 did not post itself creates exactly ONE hold; an inbound movement into BLOCKED creates one blocked-stock record
/// (origin OtherInbound). Called post-commit by the ledger (<see cref="QuarantineIntakeObserver"/>) and again by the intake
/// sweep for anything the observer missed. Exactly-once is the unique <c>(TenantId, SourceMovementId)</c> index — a second
/// caller finds the record (or hits the index) and creates nothing. A movement into DAMAGED creates nothing (pack §8 note).
/// </summary>
public sealed class QuarantineIntakeService
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineSupport _support;
    private readonly ILogger<QuarantineIntakeService> _logger;

    public QuarantineIntakeService(IQuarantineRepository repository, QuarantineSupport support, ILogger<QuarantineIntakeService> logger)
    {
        _repository = repository;
        _support = support;
        _logger = logger;
    }

    public enum IntakeTarget
    {
        None,
        Hold,
        Blocked
    }

    /// <summary>Which record a committed transaction calls for (pure: observer, sweep and tests share it).</summary>
    public static IntakeTarget TargetOf(InventoryTransaction transaction)
    {
        if (string.Equals(transaction.SourceModule, QuarantineContract.ModuleId, StringComparison.Ordinal)
            || transaction.MovementType == MovementType.Reversal
            || MovementDirectionRules.Classify(transaction.MovementType) != MovementClass.Inbound
            || transaction.ToStockStatus is null || transaction.ToWarehouseId is null || transaction.ToLocationId is null)
        {
            return IntakeTarget.None;
        }

        if (QuarantineStockEffects.HoldStatuses.Contains(transaction.ToStockStatus.Value))
        {
            return IntakeTarget.Hold;
        }

        return transaction.ToStockStatus == StockStatus.Blocked ? IntakeTarget.Blocked : IntakeTarget.None;
    }

    public static HoldSource SourceOf(MovementType type) => type switch
    {
        MovementType.GoodsReceiptPo => HoldSource.GoodsReceipt,
        MovementType.GoodsReceiptProd => HoldSource.Production,
        MovementType.CustomerReturn => HoldSource.CustomerReturn,
        _ => HoldSource.OtherInbound
    };

    /// <summary>Creates the record a transaction calls for, once. Returns true when this call created it.</summary>
    public async Task<bool> TakeInAsync(InventoryTransaction transaction, CancellationToken ct)
    {
        var target = TargetOf(transaction);
        if (target == IntakeTarget.None)
        {
            return false;
        }

        var movementId = transaction.Id.ToString("D");
        if (target == IntakeTarget.Hold
                ? await _repository.FindHoldBySourceMovementAsync(movementId, ct) is not null
                : await _repository.FindBlockedBySourceMovementAsync(movementId, ct) is not null)
        {
            return false;
        }

        var now = QuarantineSupport.Now();
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, c) =>
            {
                var year = now.UtcDateTime.Year;
                if (target == IntakeTarget.Hold)
                {
                    var hold = new QuarantineHold
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = now,
                        HoldNumber = await session.NextNumberAsync(QuarantineContract.HoldPrefix, year, c),
                        ItemId = transaction.ItemId,
                        SkuId = transaction.SkuId,
                        SkuLevel = transaction.SkuLevel,
                        LotId = transaction.LotId,
                        WarehouseId = transaction.ToWarehouseId!,
                        LocationId = transaction.ToLocationId!,
                        StockStatus = transaction.ToStockStatus!.Value,
                        Quantity = transaction.BaseQuantity,
                        BaseUomId = transaction.BaseUomId,
                        SerialIds = transaction.SerialIds is { Count: > 0 } s ? [.. s] : null,
                        Source = SourceOf(transaction.MovementType),
                        SourceMovementId = movementId,
                        SourceDocument = new HoldSourceDocument
                        {
                            Module = transaction.SourceModule,
                            Type = transaction.SourceType,
                            Id = transaction.SourceDocumentId,
                            Number = transaction.TransactionNumber
                        },
                        EnteredAtUtc = transaction.PostingDate,
                        CreatedBy = transaction.CreatedBy
                    };
                    await session.InsertHoldAsync(hold, c);
                    await session.InsertHistoryAsync([_support.History(HistorySubject.Hold, hold.Id, HistoryAction.Quarantined, now, byUserId: transaction.CreatedBy)], c);
                }
                else
                {
                    var blocked = new BlockedStock
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = now,
                        BlockedNumber = await session.NextNumberAsync(QuarantineContract.BlockedPrefix, year, c),
                        ItemId = transaction.ItemId,
                        SkuId = transaction.SkuId,
                        SkuLevel = transaction.SkuLevel,
                        LotId = transaction.LotId,
                        WarehouseId = transaction.ToWarehouseId!,
                        LocationId = transaction.ToLocationId!,
                        Quantity = transaction.BaseQuantity,
                        BaseUomId = transaction.BaseUomId,
                        SerialIds = transaction.SerialIds is { Count: > 0 } bs ? [.. bs] : null,
                        BlockedAtUtc = transaction.PostingDate,
                        Origin = BlockedOrigin.OtherInbound,
                        SourceMovementId = movementId,
                        CreatedBy = transaction.CreatedBy
                    };
                    await session.InsertBlockedAsync(blocked, c);
                    await session.InsertHistoryAsync([_support.History(HistorySubject.Blocked, blocked.Id, HistoryAction.Blocked, now, byUserId: transaction.CreatedBy)], c);
                }

                return true;
            }, ct);
            return true;
        }
        catch (QuarantineDuplicateException duplicate)
        {
            _logger.LogInformation("Intake of transaction {TransactionId} already recorded ({Index}); nothing created.", movementId, duplicate.Index);
            return false;
        }
    }
}

/// <summary>The post-commit ledger observer (MOD-0173 seam) that feeds <see cref="QuarantineIntakeService"/>.</summary>
public sealed class QuarantineIntakeObserver : IPostedMovementObserver
{
    private readonly QuarantineIntakeService _intake;

    public QuarantineIntakeObserver(QuarantineIntakeService intake) => _intake = intake;

    public Task OnPostedAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default)
        => QuarantineIntakeService.TargetOf(transaction) == QuarantineIntakeService.IntakeTarget.None
            ? Task.CompletedTask
            : _intake.TakeInAsync(transaction, cancellationToken);
}
