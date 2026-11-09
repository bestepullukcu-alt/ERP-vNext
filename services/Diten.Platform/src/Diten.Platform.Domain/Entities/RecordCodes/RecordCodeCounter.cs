using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §4.1 — one counter per (tenant, prefix, record type) (RCS-001 §5.1). Only ever incremented by one with
/// <c>$inc</c>; never decremented, never reset. No delete path.
/// </summary>
public sealed class RecordCodeCounter : TenantScopedEntity
{
    public required string Prefix { get; init; }
    public required string RecordType { get; init; }
    public long LastNumber { get; init; }
}
