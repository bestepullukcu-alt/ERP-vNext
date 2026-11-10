namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0181 permission keys — namespace <c>cycle-count.*</c>, owned by MOD-0181 (pack §14, Q-16 = A; PKS-001 §4 one owner per namespace). All
/// Tenant scope. <see cref="Implemented"/> are the keys the controllers enforce so far — exactly what the manifest declares; the evidence keys come
/// with DL-5. There is no <c>delete</c> key: a single Draft delete is
/// <see cref="CountsCancel"/>, a bulk one <see cref="CountsBulkDelete"/> (pack §10 G-0181-01, r4 02/F-6).
/// </summary>
public static class CycleCountPermissions
{
    /// <summary>Dashboard, list, detail, timeline, lines (blind: no expected quantity).</summary>
    public const string CountsRead = "cycle-count.counts.read";

    /// <summary>Open a count.</summary>
    public const string CountsCreate = "cycle-count.counts.create";

    /// <summary>Edit a Draft, assign, release (the snapshot).</summary>
    public const string CountsUpdate = "cycle-count.counts.update";

    /// <summary>Soft delete Draft counts in bulk (Tier 2).</summary>
    public const string CountsBulkDelete = "cycle-count.counts.bulk-delete";

    /// <summary><c>POST …/cancel</c> — Draft: soft delete; later: cancel state (Tier 3 <c>cancel</c>).</summary>
    public const string CountsCancel = "cycle-count.counts.cancel";

    /// <summary>DL-2: quantity entry, found line, submit.</summary>
    public const string CountsRecord = "cycle-count.counts.record";

    /// <summary>DL-3: Variance Trace, the expected / variance view (and the blind rule's "after submit" reader — pack §8.2 rule 5).</summary>
    public const string VariancesRead = "cycle-count.variances.read";

    /// <summary>DL-3: approve a variance (Tier 2; four eyes — pack §8.5).</summary>
    public const string VariancesApprove = "cycle-count.variances.approve";

    /// <summary>DL-3: reject a variance (Tier 2, a reason).</summary>
    public const string VariancesReject = "cycle-count.variances.reject";

    /// <summary>DL-3: request a recount of selected lines (Tier 3, this pack).</summary>
    public const string VariancesRecount = "cycle-count.variances.recount";

    /// <summary>DL-3: retry a Pending / reservation-refused correction (Tier 3, this pack).</summary>
    public const string VariancesPost = "cycle-count.variances.post";

    /// <summary>DL-3: resolve an IntegrityError correction (Tier 3, this pack).</summary>
    public const string VariancesResolve = "cycle-count.variances.resolve";

    /// <summary>DL-3: read the company count settings (no screen — an API permission, F-11).</summary>
    public const string SettingsRead = "cycle-count.settings.read";

    /// <summary>DL-3: change the company count settings (Tier 3 <c>configure</c>; no screen — an API permission, F-11).</summary>
    public const string SettingsConfigure = "cycle-count.settings.configure";

    /// <summary>The keys DL-1's controller enforces (pack §14 "Dilim" column).</summary>
    public static readonly IReadOnlyList<string> Dl1 = [CountsRead, CountsCreate, CountsUpdate, CountsBulkDelete, CountsCancel];

    /// <summary>DL-2 adds the entry / found line / submit key (pack §14).</summary>
    public static readonly IReadOnlyList<string> Dl2 = [CountsRecord];

    /// <summary>DL-3 adds the variance decisions, the recovery and the settings keys (pack §14).</summary>
    public static readonly IReadOnlyList<string> Dl3 =
        [VariancesRead, VariancesApprove, VariancesReject, VariancesRecount, VariancesPost, VariancesResolve, SettingsRead, SettingsConfigure];

    /// <summary>The settings keys — API permissions without a screen, also after DL-4 (pack §8.13, §14; F-11, AC-49).</summary>
    public static readonly IReadOnlyList<string> ScreenlessApi = [SettingsRead, SettingsConfigure];

    /// <summary>Every key the controllers enforce so far — exactly the manifest's.</summary>
    public static IReadOnlyList<string> Implemented => [.. Dl1, .. Dl2, .. Dl3];
}
