using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Infrastructure.Persistence.Models;

/// <summary>
/// MOD-0022 — one per-tenant, per-series, per-year counter (ESIG / SR / EXP numbers, ASSUMPTION-0022-07). Advanced with a
/// single upserting <c>FindOneAndUpdate</c> + <c>$inc</c>, so two parallel signers can never draw the same number.
/// </summary>
public sealed class ESignatureCounter
{
    [BsonId]
    public string Id { get; init; } = string.Empty;

    public Guid TenantId { get; init; }
    public string Series { get; init; } = string.Empty;
    public int Year { get; init; }
    public long Current { get; init; }

    public static string KeyFor(Guid tenantId, string series, int year) => $"{tenantId:D}:{series}:{year}";
}
