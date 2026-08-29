using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU31A — an append-only record of ONE application of the MOD-0029 default governance policy pack to a
/// tenant (GMG-QMS-SOP-0001). The pack is idempotent, so it may be applied repeatedly; each run writes a NEW row
/// rather than updating the previous one, which is why <c>PackKey</c> carries no unique index. Re-running a pack
/// that is already fully applied is a legitimate, auditable event that created nothing.
///
/// BOUNDARY: this is a governance EVIDENCE sidecar. It records what the seeder did — which policy keys were
/// created, which already existed, which diverged — and never participates in policy evaluation. A preview never
/// writes one of these rows.
/// </summary>
public sealed class DocumentGovernancePolicyPackApplication : TenantScopedEntity
{
    public required string PackKey { get; set; }
    public required string PackName { get; set; }
    public required string PackVersion { get; set; }
    public string? SopReference { get; set; }

    public DocumentGovernancePolicyPackApplicationStatus ApplicationStatus { get; set; }
        = DocumentGovernancePolicyPackApplicationStatus.Applied;

    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? AppliedByUserId { get; set; }
    public string? AppliedByRole { get; set; }

    // ── counters (mirror the seeder result) ──────────────────────────────────────────────────────────────
    public int CreatedPolicyCount { get; set; }
    public int SkippedExistingCount { get; set; }
    public int ConflictCount { get; set; }

    public List<string> WarningMessages { get; set; } = [];
    public List<string> ConflictMessages { get; set; } = [];

    // ── created policy ids, per family ───────────────────────────────────────────────────────────────────
    public List<Guid> CreatedRetentionPolicyIds { get; set; } = [];
    public List<Guid> CreatedGDocPPolicyIds { get; set; } = [];
    public List<Guid> CreatedSignaturePolicyIds { get; set; } = [];

    // ── policy keys by outcome (human-legible audit surface) ─────────────────────────────────────────────
    public List<string> CreatedPolicyKeys { get; set; } = [];
    public List<string> SkippedPolicyKeys { get; set; } = [];
    public List<string> ConflictPolicyKeys { get; set; } = [];

    /// <summary>Always false for a persisted row — a preview writes no history. Kept explicit for legibility.</summary>
    public bool PreviewOnly { get; set; }

    public string? CorrelationId { get; set; }
}
