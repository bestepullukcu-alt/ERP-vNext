using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Persistence;
using MongoDB.Driver;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// Writes REAL location documents (the MOD-0173-FU02 <c>inventory_locations</c> shape, through the same BSON mapping
/// the service uses) for ledger scenarios that need places to post to (AC-22). The ledger then resolves them through
/// the production in-process directory and its in-transaction K-13 check — nothing is stubbed. Ids are fresh UUIDs per
/// scenario (no hard-coded location id). The owner API itself is exercised by the location tests; this is a fixture,
/// so it writes the documents directly and synchronously (ASSUMPTION-LOC1-10).
/// </summary>
public static class LocationSeed
{
    public static Location Node(
        Guid tenant,
        Guid legalEntity,
        LocationType type,
        string code,
        Location? parent,
        bool? stockable = null,
        LocationStatus status = LocationStatus.Active)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            LegalEntityId = legalEntity,
            CreatedAt = DateTimeOffset.UtcNow,
            Code = code,
            CodeNormalized = LocationHierarchyRules.NormalizeCode(code),
            Name = code,
            Type = type,
            ParentId = parent?.Id,
            AncestorIds = parent is null ? [] : [.. parent.AncestorIds, parent.Id],
            Status = status,
            IsStockable = stockable ?? LocationHierarchyRules.DefaultStockable(type),
            OperatingMode = LocationHierarchyRules.MayHaveOperatingMode(type) ? LocationHierarchyRules.OperatingModeInternal : null,
            Version = 1,
            CreatedBy = Guid.Empty
        };

    public static void Insert(IMongoDatabase database, params Location[] locations)
    {
        InventoryBsonMappings.Register();
        database.GetCollection<Location>(InventoryCollections.Locations).InsertMany(locations);
    }
}
