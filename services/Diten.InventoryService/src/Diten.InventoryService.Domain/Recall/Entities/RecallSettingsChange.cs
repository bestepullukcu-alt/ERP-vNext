using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Recall.Entities;

/// <summary>
/// WP-0177-R4 (CT-R16, RECALL 1.3.0 <c>prepareSettingsChange</c>) — a PREPARED settings change of one user: the full proposal + reason and the
/// field changes it makes against <see cref="SettingsVersion"/>, kept 15 minutes. It is the MOD-0022 subject <c>recall.settings-change</c>
/// (field list <c>rs-v1</c>): the signer signs exactly this change, and <c>updateSettings</c> accepts a signature only for the same proposal on the
/// same version. Insert-only: a save moves the settings version on, so a used or stale change can never verify again — nothing is updated.
/// </summary>
public sealed class RecallSettingsChange : EntityBase
{
    /// <summary>The settings version the change applies to (0 = the company has no row yet).</summary>
    public int SettingsVersion { get; set; }

    public int DrillTargetHours { get; set; }

    public int DrillFrequencyWarningDays { get; set; }

    public Dictionary<string, decimal> EffectivenessTargetPercent { get; set; } = [];

    public int TraceMaxDepth { get; set; }

    public int TraceMaxNodes { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>The changed fields (field → old / new as the history writes them), in <c>RecallSettingsRules.Changes</c> order.</summary>
    public List<RecallSettingsFieldChange> Changes { get; set; } = [];

    public Guid SetBy { get; set; }

    public DateTimeOffset SetAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>The proposal's fingerprint (values + reason, no key): <c>updateSettings</c> compares its own body with it.</summary>
    public string ProposalFingerprint { get; set; } = string.Empty;
}

public sealed class RecallSettingsFieldChange
{
    public string Field { get; set; } = string.Empty;

    public string Old { get; set; } = string.Empty;

    public string New { get; set; } = string.Empty;
}
