namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0167 FU02 — <b>TargetCustomer</b>: a concrete membership row, but ONLY the kind a human wrote by hand
/// (D-TC). Derived membership is never written here: <see cref="MembershipMode"/> has exactly two legal values
/// (manual-include / manual-exclude) and there is deliberately no third, so the question "did this row come from the
/// rule or from a person?" is answered by the model itself rather than row by row.
/// <para>Its own aggregate and its own collection (not embedded in <see cref="Segment"/>): the cardinality of a static
/// segment is unbounded (16MB document limit), rows need row-level concurrency, and they are queried independently
/// ("which segments has this person been added to by hand?").</para>
/// <para>The referenced subject master is never read nor mutated here: the caller supplies the id, exactly as
/// <c>CampaignTarget</c> does. <see cref="SubjectDisplayName"/> is display/audit only and is explicitly NOT a source of
/// truth. Tenant-owned; no hard delete (closing a row is the soft archive lifecycle).</para>
/// </summary>
public sealed class TargetCustomer : EntityBase
{
    /// <summary>Owning segment. IMMUTABLE after create.</summary>
    public Guid SegmentId { get; set; }

    /// <summary><see cref="SegmentSubjectTypes"/>. Must equal the owning segment SubjectType (400 otherwise).</summary>
    public string SubjectType { get; set; } = string.Empty;

    /// <summary>The resolution key. The referenced master is neither read nor mutated.</summary>
    public Guid SubjectId { get; set; }

    /// <summary><see cref="SegmentMembershipModes"/> — manual-include / manual-exclude. Switching between the two is an
    /// UPDATE of this row, never a second row.</summary>
    public string MembershipMode { get; set; } = SegmentMembershipModes.ManualInclude;

    /// <summary>Display/audit only. Explicitly NOT the source of truth: a consumer resolves the name from the owning
    /// master.</summary>
    public string? SubjectDisplayName { get; set; }

    /// <summary>Free-text justification. A manual membership without a reason is not authorable.</summary>
    public string SelectionReason { get; set; } = string.Empty;

    /// <summary>At least one, each a member of <see cref="SegmentReasonCodes"/>.</summary>
    public List<string> ReasonCodes { get; set; } = new();

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Open-ended when null. EffectiveFrom/EffectiveTo are DateTimeOffset (BSON array): never both index keys
    /// and never sorted server-side (parallel-array trap).</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    public bool IsInclude()
        => string.Equals(MembershipMode, SegmentMembershipModes.ManualInclude, StringComparison.Ordinal);

    public bool IsExclude()
        => string.Equals(MembershipMode, SegmentMembershipModes.ManualExclude, StringComparison.Ordinal);

    /// <summary>Effective at the instant. Read-only helper; the resolver applies it, the row never decides.</summary>
    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);
}
