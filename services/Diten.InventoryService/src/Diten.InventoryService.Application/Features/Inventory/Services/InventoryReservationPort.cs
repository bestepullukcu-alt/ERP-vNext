using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.ValueObjects;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// <see cref="IInventoryReservationPort"/> (MOD-0173-FU03 §8.7) over the SAME <see cref="ReservationCommandService"/> the HTTP handlers
/// use. The company check is the first statement of every writing member and of the key read — before any read or write
/// (<c>InternalMovementPostingService.PostAsync</c> pattern): the repository is bound to the resolved scope, a different company would
/// silently be answered for the resolved one.
/// </summary>
public sealed class InventoryReservationPort : IInventoryReservationPort
{
    private readonly ReservationCommandService _commands;
    private readonly IInventoryReservationRepository _reservations;
    private readonly ITenantContext _tenantContext;

    public InventoryReservationPort(ReservationCommandService commands, IInventoryReservationRepository reservations, ITenantContext tenantContext)
    {
        _commands = commands;
        _reservations = reservations;
        _tenantContext = tenantContext;
    }

    public Task<Response<ReservationResponseDto>> CreateAsync(
        CreateReservationCommand request, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        RequireResolvedCompany(legalEntityId);
        return _commands.CreateAsync(WithHeaderKey(request), actorUserId, cancellationToken);
    }

    public Task<ReservationBatchResponse> CreateBatchAsync(
        IReadOnlyList<CreateReservationCommand> lines, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        RequireResolvedCompany(legalEntityId);
        return _commands.CreateBatchAsync(lines.Select(WithHeaderKey).ToList(), actorUserId, cancellationToken);
    }

    public Task<Response<ReservationResponseDto>> ReleaseAsync(
        Guid reservationId, string? ifMatch, Guid actorUserId, CancellationToken cancellationToken = default)
        => _commands.ReleaseAsync(reservationId.ToString("D"), ifMatch, actorUserId, cancellationToken);

    public async Task<ReservationViewDto?> GetAsync(Guid reservationId, CancellationToken cancellationToken = default)
        => await _reservations.FindByIdAsync(reservationId, cancellationToken) is { } reservation
            ? InventoryMapping.ToReservationView(reservation)
            : null;

    public async Task<decimal> GetScopeFreeAsync(
        Guid itemId, Guid skuId, SkuLevel skuLevel, string warehouseId, Guid? lotId = null, CancellationToken cancellationToken = default)
    {
        var sku = new SkuKey(itemId, skuId, skuLevel);
        var reserved = await _reservations.SumActiveScopeReservedAsync(sku, warehouseId, lotId, DateTimeOffset.UtcNow, cancellationToken);
        return await _reservations.ScopeAvailableOnHandAsync(sku, warehouseId, lotId, cancellationToken) - reserved;
    }

    public async Task<IReadOnlyList<ReservationKeyMatch>> FindByIdempotencyKeysAsync(
        Guid tenantId, Guid legalEntityId, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        if (tenantId != _tenantContext.TenantId)
        {
            throw new InvalidOperationException("A reservation key read must name the resolved tenant; the repository is bound to it.");
        }

        RequireResolvedCompany(legalEntityId);
        if (keys is null || keys.Count is 0 or > ReservationCommandService.MaxBatchLines)
        {
            throw new ArgumentException($"Between 1 and {ReservationCommandService.MaxBatchLines} idempotency keys are read at once.", nameof(keys));
        }

        if (keys.Any(string.IsNullOrWhiteSpace) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
        {
            throw new ArgumentException("Idempotency keys must be non-blank and distinct.", nameof(keys));
        }

        return (await _reservations.FindByIdempotencyKeysAsync(keys, cancellationToken))
            .Select(r => new ReservationKeyMatch(r.IdempotencyKey, r.Id, WireNames.ToWire(r.Status),
                r.Status == ReservationStatus.Active ? null : r.ClosedReason, InventoryMapping.ETag(r.Version)))
            .ToList();
    }

    private void RequireResolvedCompany(Guid legalEntityId)
    {
        if (legalEntityId != _tenantContext.LegalEntityId)
        {
            throw new InvalidOperationException("An in-process reservation must name the resolved legal entity; the reservation repository is bound to it.");
        }
    }

    /// <summary>In process there is no header: the body key stands for the Idempotency-Key header (the parser still requires both equal).</summary>
    private static CreateReservationCommand WithHeaderKey(CreateReservationCommand request)
        => request.HeaderIdempotencyKey is null ? request with { HeaderIdempotencyKey = request.IdempotencyKey } : request;
}
