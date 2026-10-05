namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS — the reference sets EVERY signed-in tenant user may read through
/// <c>GET api/lookups/reference-data/consumable-sets/{setCode}/published-values</c>, without
/// <c>Platform.BusinessReferenceData.Consumer.Read</c>.
///
/// <para><b>Why a list at all.</b> The route exists because the consuming services (CRM first) read reference sets with
/// the caller's own token, and only an administrator holds the Platform consumer permission. Opening the whole catalog
/// to every role would be a different decision; the list keeps the route to exactly the sets those consumers validate
/// against. A set that is not listed answers 404 <c>reference_set_not_tenant_accessible</c> and the consumer falls back
/// to its old path.</para>
///
/// <para><b>Where the list lives.</b> Configuration (<c>BusinessReferenceData:ConsumableSets</c> in appsettings.json),
/// with <see cref="DefaultConsumableSets"/> as the fallback when the key is missing or empty. Both carry the SAME codes:
/// <c>BusinessReferenceDataConsumableSetsTests</c> fails when they differ, and the CRM drift guard
/// (<c>CrmReferenceSetDriftGuardTests</c>) fails when a set code CRM consumes is missing from the configured list.</para>
///
/// <para><b>What it is NOT.</b> It says nothing about a set's scope. Whether a set is read in the caller's tenant or in
/// the reference tenant is decided by the set's own BRD metadata (<c>ScopeType</c>), never by this list.</para>
/// </summary>
public sealed class BusinessReferenceDataConsumableSetsOptions
{
    public const string SectionName = "BusinessReferenceData";

    /// <summary>Bound from <c>BusinessReferenceData:ConsumableSets</c>. Empty → <see cref="DefaultConsumableSets"/>.</summary>
    public List<string> ConsumableSets { get; set; } = [];

    /// <summary>
    /// Every reference set CRM consumes (Diten.CrmService, measured 2026-10-02 from the IReferenceDataValidator /
    /// IReferenceMetadataReader / IReferenceDataCatalogReader call sites) plus the sets the CRM Web screens read for their
    /// dropdowns (Diten.Web Controllers/CRM through CrmReferenceSetReader). Keep in step with appsettings.json.
    /// </summary>
    public static IReadOnlyList<string> DefaultConsumableSets { get; } =
    [
        // Account — AccountReferenceValidation
        "account-type",
        "account-status",
        "account-category",
        // Contact — ContactReferenceValidation (location + professional sets are shared with Account)
        "contact-type",
        "contact-status",
        "country",
        "city",
        "district",
        "professional-title",
        "medical-specialty",
        "department-type",
        "gender",
        // Account-contact link + account relationship (incl. their import/export handlers)
        "contact-role",
        "account-relationship-type",
        "account-relationship-status",
        // Contact workbook — ContactWorkbookSchema
        "preferred-language",
        "phone-country-code",
        // Contact availability — ContactAvailabilityReferenceSets
        "contact-availability-type",
        "contact-availability-source",
        "contact-availability-status",
        // Scope axes — Campaign / CyclePeriod / CycleCapacity / StrategyTemplate / Territory business unit / Claims
        "COUNTRY_CODES",
        "business-unit",
        // Claims v2 — ClaimReferenceSets
        "country-content-languages",
        "claim-country-closure-reason",
        "claim-adaptation-type",
        // Territory — TerritoryReferenceSets
        "territory-level",
        "territory-model-status",
        "territory-node-status",
        "territory-assignment-status",
        "territory-assignment-source",
        "territory-resource-role",
        "territory-rule-type",
        "territory-conflict-policy",
        "territory-coverage-scope",
        "business-scope-type",
        // Visit frequency policy — VisitFrequencyPolicyReferenceSets (readiness on the contract)
        "visit-frequency-target-type",
        "visit-frequency-type",
        "visit-frequency-period-type",
        "visit-frequency-source",
        "visit-frequency-status",
        // CRM Web dropdowns (Diten.Web Controllers/CRM, step 2 — read only by the Web, not validated by CrmService):
        // Claims v2 evidence-type lookup, Territory node planning-center type, Chain Template moderator picker.
        "evidence-type",
        "planning-center-type",
        "content-moderator-role"
    ];

    /// <summary>The configured list when it names at least one set, otherwise the code default.</summary>
    public IReadOnlyList<string> EffectiveConsumableSets()
    {
        var configured = (ConsumableSets ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToList();

        return configured.Count > 0 ? configured : DefaultConsumableSets;
    }

    /// <summary>
    /// Normalizes the requested code (trim, case-insensitive) against the effective list and returns the LISTED spelling,
    /// because BRD set codes are stored and matched case-sensitively (<c>ACCOUNT-TYPE</c> → <c>account-type</c>,
    /// <c>country_codes</c> → <c>COUNTRY_CODES</c>).
    /// </summary>
    public bool TryResolve(string? requestedSetCode, out string setCode)
    {
        setCode = string.Empty;
        if (string.IsNullOrWhiteSpace(requestedSetCode))
        {
            return false;
        }

        var requested = requestedSetCode.Trim();
        var match = EffectiveConsumableSets()
            .FirstOrDefault(code => string.Equals(code, requested, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        setCode = match;
        return true;
    }
}
