namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public enum SeedDecision
{
    /// <summary>Marker already present → seed has run once; never touch the collection again.</summary>
    Skip,

    /// <summary>No marker and the collection is empty (fresh install) → insert defaults, then set the marker.</summary>
    SeedAndMark,

    /// <summary>No marker but the collection already has curated data → preserve it (no seed), just set the marker.</summary>
    MarkOnly
}

/// <summary>
/// Pure decision rule shared by all bootstrap-only seeds. Encapsulates the three-state marker logic so it can be
/// unit-tested without MongoDB. See <see cref="SeedMarkerStore"/>.
/// </summary>
public static class BootstrapSeedPolicy
{
    public static SeedDecision Decide(bool markerExists, bool hasLiveRecords)
    {
        if (markerExists)
        {
            return SeedDecision.Skip;
        }

        return hasLiveRecords ? SeedDecision.MarkOnly : SeedDecision.SeedAndMark;
    }
}
