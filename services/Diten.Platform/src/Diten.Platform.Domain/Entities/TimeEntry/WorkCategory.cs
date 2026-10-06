using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.TimeEntry;

/// <summary>
/// MOD-0280-FU01 (pack §4.6, D10) — the tenant catalogue of non-task work. Deactivated, never deleted: a category
/// referenced by old entries must still resolve its label.
///
/// <para>The label is <see cref="LabelResourceKey"/> (set only by the recommended install, resolved from resx in
/// seven languages) OR <see cref="LabelText"/> (typed by the tenant, shown as typed) — the TaskClosureOutcome
/// mechanism (R6).</para>
/// </summary>
public sealed class WorkCategory : TenantScopedEntity
{
    /// <summary><c>^[A-Z][A-Z0-9_]{1,31}$</c>, tenant-unique, immutable after create.</summary>
    public required string Code { get; set; }

    public string? LabelText { get; set; }

    public string? LabelResourceKey { get; set; }

    /// <summary>Max 500.</summary>
    public string? Description { get; set; }

    public bool CountsAsWork { get; set; } = true;

    /// <summary>0…999.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
