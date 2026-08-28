using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU13 — the 30-day validity control for an urgent / temporary instruction (GMG-QMS-SOP-0001 §6.1 class 7).
/// A temporary instruction is valid for a MAXIMUM of 30 calendar days and at expiry SHALL transition to EXACTLY ONE of:
/// incorporated into a permanent controlled document; formally withdrawn; replaced by a newly approved temporary
/// instruction under a NEW identifier; or suspended because no valid replacement exists. An expired temporary
/// instruction SHALL NEVER remain operational by default — expiry without an action raises a suspension case.
/// Never hard-deleted.
/// </summary>
public sealed class TemporaryInstructionControl : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }

    public TemporaryInstructionStatus TemporaryInstructionStatus { get; set; } = TemporaryInstructionStatus.Active;

    public DateTimeOffset ValidFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ValidUntil { get; set; }

    /// <summary>SOP hard ceiling — 30 calendar days.</summary>
    public int MaxValidityDays { get; set; } = 30;

    public TemporaryInstructionExpiryAction? ExpiryAction { get; set; }
    public string? ExpiryActionEvidenceReference { get; set; }

    /// <summary>Required when the expiry action is ReplaceWithNewTemporary — the new instruction's register entry.</summary>
    public Guid? ReplacementRegisterEntryId { get; set; }

    /// <summary>The suspension case raised when the instruction expired with no action, or SuspendNoReplacement.</summary>
    public Guid? SuspensionCaseId { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
