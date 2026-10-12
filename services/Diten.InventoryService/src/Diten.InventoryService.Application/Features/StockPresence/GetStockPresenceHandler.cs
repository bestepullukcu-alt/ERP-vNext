using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.StockPresence;

/// <summary>
/// <c>getStockPresence</c>: <c>X-Tenant-Id</c> / <c>skuId</c> must be UUIDs and <c>skuLevel</c> a 1.1.0 value (else 400
/// <c>INVALID_REQUEST</c>); the read covers ONLY that tenant and ALL its legal entities and stock statuses
/// (<see cref="IStockPresenceReader"/>); a store that cannot be read is 503 <c>INVENTORY_UNAVAILABLE</c> (never a partial
/// answer, <c>coverage</c> is always <c>COMPLETE</c>). Every call is audited (key identifier, tenant, SKU, level, result,
/// correlation id); a 200 is only given when its audit row is written — no audit, no answer (503).
/// </summary>
public sealed class GetStockPresenceHandler : IRequestHandler<GetStockPresenceQuery, Response<StockPresenceDto>>
{
    private readonly IStockPresenceReader _reader;
    private readonly IInternalAccessAuditWriter _audit;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<GetStockPresenceHandler> _logger;

    public GetStockPresenceHandler(
        IStockPresenceReader reader, IInternalAccessAuditWriter audit, ICorrelationContext correlation, ILogger<GetStockPresenceHandler> logger)
    {
        _reader = reader;
        _audit = audit;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task<Response<StockPresenceDto>> Handle(GetStockPresenceQuery request, CancellationToken cancellationToken)
    {
        var tenantOk = WireParsing.TryParseUuid(request.TenantId, out var tenantId);
        var skuOk = WireParsing.TryParseUuid(request.SkuId, out var skuId);
        var levelOk = StockPresenceContract.TryParseLevel(request.SkuLevel, out var level);
        var audit = new InternalServiceAccessAuditRecord
        {
            Action = InternalServiceAccessAuditRecord.StockPresenceAction,
            KeyIdentifier = request.KeyIdentifier,
            TenantId = tenantOk ? tenantId : null,
            SkuId = skuOk ? skuId : null,
            SkuLevel = levelOk ? request.SkuLevel : null,
            CorrelationId = _correlation.CorrelationId,
            OccurredAt = DateTimeOffset.UtcNow
        };

        if (!tenantOk || !skuOk || !levelOk)
        {
            var errors = new List<string>();
            if (!tenantOk) errors.Add($"{InventoryContract.TenantHeader} is required and must be a UUID.");
            if (!skuOk) errors.Add("skuId is required and must be a UUID.");
            if (!levelOk) errors.Add("skuLevel is required and must be one of Gsku, Lsku, FinishedGood, Item.");
            await TryAuditAsync(audit, 400, "invalid-request", cancellationToken);
            return Response<StockPresenceDto>.Fail(InventoryErrorCodes.InvalidRequest, errors, 400);
        }

        bool present;
        DateTimeOffset asOf;
        try
        {
            present = await _reader.HasNonZeroStockAsync(tenantId, skuId, level, cancellationToken);
            asOf = DateTimeOffset.UtcNow;
            audit.StatusCode = 200;
            audit.Outcome = present ? "present" : "absent";
            await _audit.WriteAsync(audit, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Stock presence unavailable. Key={KeyIdentifier} Tenant={TenantId} Sku={SkuId} Level={SkuLevel} Correlation={CorrelationId}",
                request.KeyIdentifier, tenantId, skuId, request.SkuLevel, _correlation.CorrelationId);
            await TryAuditAsync(audit, 503, "unavailable", cancellationToken);
            return Response<StockPresenceDto>.Fail(InventoryErrorCodes.InventoryUnavailable, "The inventory store could not be read.", 503);
        }

        _logger.LogInformation("Stock presence answered. Key={KeyIdentifier} Tenant={TenantId} Sku={SkuId} Level={SkuLevel} Present={Present} Correlation={CorrelationId}",
            request.KeyIdentifier, tenantId, skuId, request.SkuLevel, present, _correlation.CorrelationId);
        return Response<StockPresenceDto>.Success(new StockPresenceDto
        {
            SkuId = skuId.ToString("D"),
            SkuLevel = request.SkuLevel!,
            HasNonZeroStock = present,
            Coverage = StockPresenceContract.CoverageComplete,
            AsOf = InventoryMapping.Utc(asOf)
        });
    }

    private async Task TryAuditAsync(InternalServiceAccessAuditRecord audit, int status, string outcome, CancellationToken cancellationToken)
    {
        audit.StatusCode = status;
        audit.Outcome = outcome;
        try
        {
            await _audit.WriteAsync(audit, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Stock presence audit could not be written. Status={Status} Correlation={CorrelationId}", status, audit.CorrelationId);
        }
    }
}
