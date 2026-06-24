namespace Diten.MdmService.Application.Features.Lookups;

// MOD-0220 FULL — MDM-local reference data. Faz 1 ships an authoritative operator-seed set in code;
// a full CRUD management screen + DB-backed collections are DEFERRED. Shape mirrors the Platform
// LookupOptionDto ({code, name, value, sortOrder}) so the frontend can consume both surfaces uniformly.
public sealed record LookupOptionDto(string Code, string Name, string Value, int SortOrder);

public static class LegalEntityLookupTypes
{
    public const string LegalForm = "legal-form";
    public const string OrganizationRole = "organization-role";
    public const string ControlType = "control-type";
    public const string AccountingStandard = "accounting-standard";
    public const string TaxRegime = "tax-regime";
}

public static class LegalEntityReferenceData
{
    // Codes are UPPERCASE and stable (they are persisted on LegalEntity); display names are operator-facing.
    // OrganizationRole BRANCH/REPOFFICE codes drive the parent-required rule (see LegalEntityRules).
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<LookupOptionDto>> Sets =
        new Dictionary<string, IReadOnlyList<LookupOptionDto>>(StringComparer.OrdinalIgnoreCase)
        {
            [LegalEntityLookupTypes.LegalForm] = Build(
                ("CORPORATION", "Corporation"),
                ("LLC", "Limited Liability Company"),
                ("PARTNERSHIP", "Partnership"),
                ("SOLEPROP", "Sole Proprietorship"),
                ("BRANCH", "Branch"),
                ("REPOFFICE", "Representative Office")),

            [LegalEntityLookupTypes.OrganizationRole] = Build(
                ("LEGALENTITY", "Legal Entity"),
                ("BRANCH", "Branch"),
                ("REPOFFICE", "Representative Office"),
                ("DIVISION", "Division")),

            [LegalEntityLookupTypes.ControlType] = Build(
                ("SUBSIDIARY", "Subsidiary"),
                ("ASSOCIATE", "Associate"),
                ("JOINTVENTURE", "Joint Venture"),
                ("BRANCH", "Branch")),

            [LegalEntityLookupTypes.AccountingStandard] = Build(
                ("IFRS", "IFRS"),
                ("USGAAP", "US GAAP"),
                ("LOCALGAAP", "Local GAAP")),

            [LegalEntityLookupTypes.TaxRegime] = Build(
                ("STANDARD", "Standard"),
                ("SIMPLIFIED", "Simplified"),
                ("EXEMPT", "Exempt"),
                ("GROUP", "Group Taxation")),
        };

    public static bool TryGet(string? type, out IReadOnlyList<LookupOptionDto> options)
    {
        if (!string.IsNullOrWhiteSpace(type) && Sets.TryGetValue(type.Trim(), out var found))
        {
            options = found;
            return true;
        }

        options = Array.Empty<LookupOptionDto>();
        return false;
    }

    private static IReadOnlyList<LookupOptionDto> Build(params (string Code, string Name)[] items)
        => items.Select((x, i) => new LookupOptionDto(x.Code, x.Name, x.Code, i + 1)).ToList();
}
