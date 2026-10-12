namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0175 permission keys — namespace <c>quarantine.*</c>, owned by MOD-0175 (pack §14; PKS-001 §4 one owner per
/// namespace). Tier-3 verbs registered by the pack (PKS-001 §5): <c>review</c>, <c>dispose</c>, <c>unblock</c>, <c>block</c>,
/// <c>withdraw</c>, <c>apply</c>, <c>manage</c>. All Tenant scope (every route is a tenant route).
/// </summary>
public static class QuarantinePermissions
{
    public const string HoldsRead = "quarantine.holds.read";

    public const string HoldsCreate = "quarantine.holds.create";

    public const string HoldsReview = "quarantine.holds.review";

    public const string DecisionsCreate = "quarantine.decisions.create";

    public const string DecisionsWithdraw = "quarantine.decisions.withdraw";

    public const string DecisionsApply = "quarantine.decisions.apply";

    public const string BlockedDispose = "quarantine.blocked.dispose";

    public const string BlockedUnblock = "quarantine.blocked.unblock";

    public const string BlockedBlock = "quarantine.blocked.block";

    public const string SettingsManage = "quarantine.settings.manage";

    public static readonly IReadOnlyList<string> All =
    [
        HoldsRead, HoldsCreate, HoldsReview, DecisionsCreate, DecisionsWithdraw, DecisionsApply, BlockedDispose, BlockedUnblock, BlockedBlock,
        SettingsManage
    ];
}
