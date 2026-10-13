namespace Diten.MdmService.Application.Contracts.ReferenceData;

/// <summary>
/// WP-VW-W4 (W4-BE-b2) — the active value codes of a MOD-0048 business reference set as the caller's tenant sees it
/// (the Platform consumable-sets route, caller's <c>Authorization</c> + <c>X-Tenant-Id</c>; the CRM
/// <c>GatewayReferenceDataValidator</c> rule). Null when the set cannot be read — the caller fails closed (503).
/// </summary>
public interface IMdmReferenceValueReader
{
    Task<IReadOnlySet<string>?> ActiveCodesAsync(string setCode, CancellationToken ct);
}

/// <summary>WP-VW-W4 (W4-BE-b2) — "does the caller hold this permission?" (the MDM <c>PermissionAuthorizationHandler</c>
/// rule: a <c>permission</c> claim, or a platform admin). Used for what a read SHOWS (another rep's proposal), never instead
/// of the controller's <c>[HasPermission]</c>.</summary>
public interface IMdmPermissionContext
{
    bool Has(string permission);
}
