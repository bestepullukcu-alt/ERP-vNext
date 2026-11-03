using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// MOD-0173-FU03 §8.7 — the in-process reservation port exactly as MOD-0178 will get it: resolved from the REAL host's DI in a scope whose
/// Tenant + LE + user are set like the scope middleware sets them (a command of the caller), with its own correlation id. Every call gets a
/// fresh scope, so the scope-bound repositories see the resolved scope of that call.
/// </summary>
public sealed class PortWorld
{
    private readonly InventoryApiFactory _factory;

    public PortWorld(InventoryApiFactory factory, ScopedReservationWorld world)
        : this(factory, world.Ledger.Tenant, world.Ledger.LegalEntity, world.Ledger.User)
    {
    }

    public PortWorld(InventoryApiFactory factory, Guid tenant, Guid legalEntity, Guid user)
    {
        _factory = factory;
        Tenant = tenant;
        LegalEntity = legalEntity;
        User = user;
    }

    public Guid Tenant { get; }
    public Guid LegalEntity { get; }
    public Guid User { get; }

    public async Task<T> Use<T>(Func<IInventoryReservationPort, Task<T>> call)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(Tenant, LegalEntity, User, LegalEntitySources.Token);
        scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
        return await call(scope.ServiceProvider.GetRequiredService<IInventoryReservationPort>());
    }

    /// <summary>A port line for the world's SKU (no header: the port uses the body key).</summary>
    public static CreateReservationCommand Line(
        ScopedReservationWorld w, string quantity, string? warehouseId = null, string? lotId = null, string? key = null, string? expiresAt = null)
        => new()
        {
            IdempotencyKey = key ?? "port-" + Guid.NewGuid().ToString("N"),
            ItemId = w.Ledger.Item.ToString(),
            SkuId = w.Ledger.Sku.ToString(),
            SkuLevel = WireNames.ToWire(w.Ledger.Level),
            Quantity = quantity,
            ReservationType = "PICK",
            SourceModule = "MOD-0178",
            SourceDocumentId = "OB-" + Guid.NewGuid().ToString("N")[..6],
            SourceLineId = "1",
            WarehouseId = warehouseId,
            LotId = lotId,
            ExpiresAt = expiresAt
        };
}
