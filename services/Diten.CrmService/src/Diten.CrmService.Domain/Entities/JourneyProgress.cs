namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-SB-3b (DESIGN-SB-3 §3.2) — where ONE doctor stands on ONE product's journey: the stage the next visit tells, how
/// many times the journey was completed (<see cref="Cycle"/>) and how many times the product was told
/// (<see cref="ExposureCount"/>, the weighted rotation's input).
/// <para><b>Key:</b> (TenantId, <see cref="ContactId"/>, <see cref="ProductId"/>, <see cref="JourneyId"/>) — unique in
/// the database. A line that switches to another journey starts a NEW key from stage 0; the old row stays as history.</para>
/// <para>SB-3b only READS it (the visit content resolver). The single write is <see cref="Advance"/>, called by the
/// visit completion of SB-3c — there is no endpoint that writes it.</para>
/// </summary>
public sealed class JourneyProgress : EntityBase
{
    /// <summary>The doctor (CRM Contact).</summary>
    public Guid ContactId { get; set; }

    /// <summary>The MDM global product the strategy line promotes.</summary>
    public Guid ProductId { get; set; }

    /// <summary>The product's ContentEngagementJourney (the strategy line's journey).</summary>
    public Guid JourneyId { get; set; }

    /// <summary>The NEXT stage, 0-based over the journey's ordered active stages.</summary>
    public int CurrentStageIndex { get; set; }

    public int? LastCompletedStageIndex { get; set; }

    public DateTimeOffset? LastCompletedAt { get; set; }

    public Guid? LastVisitReportId { get; set; }

    /// <summary>How many times the journey wrapped back to its first stage (S3-7).</summary>
    public int Cycle { get; set; }

    /// <summary>How many visits told this product on this journey (the rotation input, S3-5).</summary>
    public int ExposureCount { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>True when the stored index no longer fits the journey (the journey lost stages): the reader then starts
    /// from stage 0 and says so (<c>stage_index_reset</c>) — the stored value is not rewritten by a read.</summary>
    public bool IsStageIndexStale(int stageCount)
        => stageCount > 0 && (CurrentStageIndex < 0 || CurrentStageIndex >= stageCount);

    /// <summary>The stage the next visit tells on a journey of <paramref name="stageCount"/> stages.</summary>
    public int EffectiveStageIndex(int stageCount) => IsStageIndexStale(stageCount) ? 0 : CurrentStageIndex;

    /// <summary>
    /// A completed visit told this product: the current stage becomes the last completed one, the cursor moves to the
    /// next stage and — after the last stage — back to the first with <see cref="Cycle"/> + 1 (S3-7, no "end of journey"
    /// stop). <see cref="ExposureCount"/> + 1.
    /// </summary>
    public void Advance(int stageCount, Guid? visitReportId, DateTimeOffset at)
    {
        if (stageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stageCount), "A journey without active stages cannot advance.");
        }

        var current = EffectiveStageIndex(stageCount);
        LastCompletedStageIndex = current;
        LastCompletedAt = at;
        LastVisitReportId = visitReportId;
        CurrentStageIndex = current + 1;
        if (CurrentStageIndex >= stageCount)
        {
            CurrentStageIndex = 0;
            Cycle++;
        }

        ExposureCount++;
        UpdatedAt = at;
    }

    /// <summary>The stage reached from <paramref name="stageIndex"/> after <paramref name="advances"/> more visits on a
    /// journey of <paramref name="stageCount"/> stages — the same wrap-around as <see cref="Advance"/> (used to project
    /// visits that are planned but not yet completed).</summary>
    public static int StageAfter(int stageIndex, int advances, int stageCount)
        => stageCount <= 0 ? 0 : (Math.Max(0, stageIndex) + Math.Max(0, advances)) % stageCount;
}
