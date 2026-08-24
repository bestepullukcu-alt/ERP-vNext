namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// Exact entitlement grant profile for Product Abbreviation Register permissions that are
/// published by the shared Product / Item / SKU Master manifest.
/// </summary>
public static class ProductAbbreviationEntitlementGrantProfile
{
    public const string ModuleCode = "product-item-sku-master";
    public const string PermissionPrefix = "mdm.product-abbreviations.";

    public const string Read = PermissionPrefix + "read";
    public const string Request = PermissionPrefix + "request";
    public const string Cancel = PermissionPrefix + "cancel";
    public const string Approve = PermissionPrefix + "approve";
    public const string Reject = PermissionPrefix + "reject";
    public const string Correct = PermissionPrefix + "correct";
    public const string Retire = PermissionPrefix + "retire";
    public const string Audit = PermissionPrefix + "audit";

    public const string RequesterRole = "ProductAbbreviationRequester";
    public const string StewardRole = "ProductAbbreviationSteward";
    public const string ApproverRole = "ProductAbbreviationApprover";
    public const string AuditorRole = "ProductAbbreviationAuditor";

    public static readonly IReadOnlySet<string> PermissionKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Read,
            Request,
            Cancel,
            Approve,
            Reject,
            Correct,
            Retire,
            Audit
        };

    public static readonly IReadOnlyList<ProductAbbreviationRoleGrantTemplate> DedicatedRoles =
    [
        new(
            RequesterRole,
            "Product Abbreviation Requester",
            "Requests product abbreviations and may cancel only their own requested records.",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Read, Request, Cancel }),
        new(
            StewardRole,
            "Product Abbreviation Steward",
            "Maintains and retires product abbreviations without checker authority.",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Read, Request, Correct, Cancel, Retire }),
        new(
            ApproverRole,
            "Product Abbreviation Approver",
            "Approves or rejects product abbreviation requests subject to maker-checker controls.",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Read, Approve, Reject }),
        new(
            AuditorRole,
            "Product Abbreviation Auditor",
            "Reads product abbreviations and their evidence without mutation authority.",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Read, Audit })
    ];

    public static bool AppliesTo(string normalizedModuleCode, IEnumerable<string> permissionKeys)
        => string.Equals(normalizedModuleCode, ModuleCode, StringComparison.OrdinalIgnoreCase)
           && permissionKeys.Any(IsProductAbbreviationKey);

    public static void ValidateExactPermissionSet(IEnumerable<string> permissionKeys)
    {
        var supplied = permissionKeys
            .Where(IsProductAbbreviationKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!supplied.SetEquals(PermissionKeys))
        {
            throw new InvalidOperationException(
                "Product Abbreviation entitlement reconciliation requires the exact eight-key permission set.");
        }
    }

    public static bool IsProductAbbreviationKey(string? permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey)
           && permissionKey.StartsWith(PermissionPrefix, StringComparison.OrdinalIgnoreCase);
}

public sealed record ProductAbbreviationRoleGrantTemplate(
    string RoleName,
    string DisplayName,
    string Description,
    IReadOnlySet<string> PermissionKeys);
