using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.RecordCodes;

namespace Diten.Platform.Domain.Entities.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §4.2 — the append-only ledger of issued codes. <see cref="BaseEntity.Id"/> is the allocation id (a new Guid
/// per attempt; the audit outbox key derives from it). One row per (tenant, caller service, idempotency key) and per
/// (tenant, code). Never updated, replaced or deleted.
/// </summary>
public sealed class RecordCodeAllocation : TenantScopedEntity
{
    public required string CallerService { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string RecordType { get; init; }

    /// <summary>The scope with the default applied (a v0 body without <c>prefixScope</c> is stored as LegalEntity).</summary>
    public required RecordCodePrefixScope PrefixScope { get; init; }

    public Guid? LegalEntityId { get; init; }
    public required string Prefix { get; init; }
    public required long Sequence { get; init; }
    public required string Code { get; init; }
    public required Guid OnBehalfOfUserId { get; init; }
    public required string CorrelationId { get; init; }
    /// <summary>UTC (<see cref="DateTimeKind.Utc"/>); a BSON date. <c>DateTime</c>, not the pack §4 <c>DateTimeOffset</c>: owner decision B, 2026-11-02.</summary>
    public required DateTime AllocatedAtUtc { get; init; }
}
