using System.Text.Json;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.LegalEntity;

// MOD-0220 FULL — shared write payload for Create + Update (flat, bound directly from the request body).
// OperationalStatus is intentionally absent: it is owned by the lifecycle transitions (activate/suspend/archive),
// not free-form edits. CompletenessScore is computed server-side. Enum-valued fields arrive as strings
// (e.g. "Registered", "Active") and are parsed case-insensitively, so no JSON enum-converter config is required.
public sealed record LegalEntityWriteRequest(
    // Identity
    string Code,
    string LegalName,
    string? DisplayName,
    string LegalFormCode,
    string OrganizationRoleCode,
    // Statutory & Tax
    string? RegistrationNumber,
    string? TaxId,
    string CountryCode,
    string? StatutoryStatus,
    // Structure
    Guid? ParentLegalEntityId,
    decimal? OwnershipPercent,
    string? ControlTypeCode,
    // Finance
    string? FiscalYearVariant,
    string? AccountingStandardCode,
    string? TaxRegimeCode,
    string BaseCurrencyCode,
    // Addresses & Contacts
    string RegisteredAddressJson,
    string? CorrespondenceAddressJson,
    string? OfficialEmail,
    string? OfficialPhone,
    string? Website,
    // Governance (subset — OperationalStatus excluded by design)
    string? ApprovalStatus,
    DateTimeOffset? ReviewDueUtc,
    string? SourceSystem,
    string? LegacyCode,
    // Evidence
    string? EvidenceStatus);

public sealed record LegalEntityDetailDto(
    Guid LegalEntityId,
    string Code,
    string LegalName,
    string? DisplayName,
    string LegalFormCode,
    string OrganizationRoleCode,
    string? RegistrationNumber,
    string? TaxId,
    string CountryCode,
    string StatutoryStatus,
    Guid? ParentLegalEntityId,
    decimal? OwnershipPercent,
    string? ControlTypeCode,
    string? FiscalYearVariant,
    string? AccountingStandardCode,
    string? TaxRegimeCode,
    string BaseCurrencyCode,
    string RegisteredAddressJson,
    string? CorrespondenceAddressJson,
    string? OfficialEmail,
    string? OfficialPhone,
    string? Website,
    string OperationalStatus,
    string ApprovalStatus,
    DateTimeOffset? ReviewDueUtc,
    string? SourceSystem,
    string? LegacyCode,
    string EvidenceStatus,
    int? CompletenessScore,
    // Back-compat: the original slice exposed LifecycleState; kept = OperationalStatus so existing callers don't break.
    string LifecycleState,
    bool Referenceable,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

// Minimal cross-service shape consumed by Platform's MdmLegalEntityReferenceValidator (OrgUnit FK).
// LifecycleState MUST stay "ACTIVE" for referenceable entities — do not change without updating Platform.
public sealed record LegalEntityLookupDto(
    Guid LegalEntityId,
    string LegalName,
    string? DisplayName,
    string LifecycleState,
    bool Referenceable);

public static class LegalEntityRules
{
    // Org roles whose entity must declare an owning parent (plan rule). Matches OrganizationRole seed codes.
    private static readonly HashSet<string> ParentRequiredRoleCodes =
        new(StringComparer.OrdinalIgnoreCase) { "BRANCH", "REPOFFICE" };

    public static bool RequiresParent(string? organizationRoleCode)
        => !string.IsNullOrWhiteSpace(organizationRoleCode)
           && ParentRequiredRoleCodes.Contains(organizationRoleCode.Trim());

    // Hard-required fields that drive CompletenessScore (Faz 1). Parent counts only when the role requires it.
    public static int ComputeCompleteness(Domain.Entities.LegalEntity e)
    {
        var required = new List<bool>
        {
            !string.IsNullOrWhiteSpace(e.Code),
            !string.IsNullOrWhiteSpace(e.LegalName),
            !string.IsNullOrWhiteSpace(e.LegalFormCode),
            !string.IsNullOrWhiteSpace(e.OrganizationRoleCode),
            !string.IsNullOrWhiteSpace(e.CountryCode),
            !string.IsNullOrWhiteSpace(e.BaseCurrencyCode),
            !string.IsNullOrWhiteSpace(e.RegisteredAddressJson)
        };

        if (RequiresParent(e.OrganizationRoleCode))
        {
            required.Add(e.ParentLegalEntityId.HasValue && e.ParentLegalEntityId.Value != Guid.Empty);
        }

        var filled = required.Count(x => x);
        return (int)Math.Round(100.0 * filled / required.Count);
    }
}

public static class LegalEntityMappings
{
    // Applies a write request onto an entity (create or update). Trims strings, parses enum strings, recomputes
    // CompletenessScore. Does NOT touch OperationalStatus (lifecycle-owned) or identity/audit fields.
    public static void Apply(Domain.Entities.LegalEntity entity, LegalEntityWriteRequest r)
    {
        entity.Code = r.Code.Trim();
        entity.LegalName = r.LegalName.Trim();
        entity.DisplayName = Clean(r.DisplayName);
        entity.LegalFormCode = r.LegalFormCode.Trim();
        entity.OrganizationRoleCode = r.OrganizationRoleCode.Trim();

        entity.RegistrationNumber = Clean(r.RegistrationNumber);
        entity.TaxId = Clean(r.TaxId);
        entity.CountryCode = r.CountryCode.Trim();
        entity.StatutoryStatus = ParseEnum(r.StatutoryStatus, LegalEntityStatutoryStatus.Registered);

        entity.ParentLegalEntityId = r.ParentLegalEntityId == Guid.Empty ? null : r.ParentLegalEntityId;
        entity.OwnershipPercent = r.OwnershipPercent;
        entity.ControlTypeCode = Clean(r.ControlTypeCode);

        entity.FiscalYearVariant = Clean(r.FiscalYearVariant);
        entity.AccountingStandardCode = Clean(r.AccountingStandardCode);
        entity.TaxRegimeCode = Clean(r.TaxRegimeCode);
        entity.BaseCurrencyCode = r.BaseCurrencyCode.Trim();

        entity.RegisteredAddressJson = r.RegisteredAddressJson.Trim();
        entity.CorrespondenceAddressJson = Clean(r.CorrespondenceAddressJson);
        entity.OfficialEmail = Clean(r.OfficialEmail);
        entity.OfficialPhone = Clean(r.OfficialPhone);
        entity.Website = Clean(r.Website);

        entity.ApprovalStatus = ParseEnum(r.ApprovalStatus, LegalEntityApprovalStatus.Draft);
        entity.ReviewDueUtc = r.ReviewDueUtc;
        entity.SourceSystem = Clean(r.SourceSystem);
        entity.LegacyCode = Clean(r.LegacyCode);

        entity.EvidenceStatus = ParseEnum(r.EvidenceStatus, LegalEntityEvidenceStatus.NotStarted);

        entity.CompletenessScore = LegalEntityRules.ComputeCompleteness(entity);
    }

    public static LegalEntityDetailDto ToDetailDto(Domain.Entities.LegalEntity e)
        => new(
            e.Id,
            e.Code,
            e.LegalName,
            e.DisplayName,
            e.LegalFormCode,
            e.OrganizationRoleCode,
            e.RegistrationNumber,
            e.TaxId,
            e.CountryCode,
            e.StatutoryStatus.ToString().ToUpperInvariant(),
            e.ParentLegalEntityId,
            e.OwnershipPercent,
            e.ControlTypeCode,
            e.FiscalYearVariant,
            e.AccountingStandardCode,
            e.TaxRegimeCode,
            e.BaseCurrencyCode,
            e.RegisteredAddressJson,
            e.CorrespondenceAddressJson,
            e.OfficialEmail,
            e.OfficialPhone,
            e.Website,
            e.OperationalStatus.ToString().ToUpperInvariant(),
            e.ApprovalStatus.ToString().ToUpperInvariant(),
            e.ReviewDueUtc,
            e.SourceSystem,
            e.LegacyCode,
            e.EvidenceStatus.ToString().ToUpperInvariant(),
            e.CompletenessScore,
            e.OperationalStatus.ToString().ToUpperInvariant(),
            e.IsReferenceable,
            e.Version,
            e.CreatedAt,
            e.UpdatedAt);

    public static LegalEntityLookupDto ToLookupDto(Domain.Entities.LegalEntity e)
        => new(
            e.Id,
            e.LegalName,
            e.DisplayName,
            e.OperationalStatus.ToString().ToUpperInvariant(),
            e.IsReferenceable);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum
        => !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
}
