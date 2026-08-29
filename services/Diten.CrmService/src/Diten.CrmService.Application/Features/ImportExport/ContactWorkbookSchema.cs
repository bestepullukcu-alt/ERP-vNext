using Diten.CrmService.Application.Features.Contact;

namespace Diten.CrmService.Application.Features.ImportExport;

/// <summary>
/// MOD-0150 Contact Import/Export Task 1 — the SINGLE SOURCE of the XLSX workbook shape (sheet names, column order,
/// column→MOD-0048 set bindings, text-formatted and system columns). The template writer and the export writer both
/// read this type, so a file produced by export always round-trips into the (future, Task 2) import reader.
///
/// Boundary: reference VALUES are never listed here — only the MOD-0048 <c>setCode</c> a column binds to. Values are
/// read live through <see cref="Common.ReferenceValidation.IReferenceDataCatalogReader"/>; there is no CRM local seed
/// and no hardcoded fallback list. AccountRelationship is deliberately absent (separate follow-up).
/// </summary>
public static class ContactWorkbookSchema
{
    // ---- Sheets ----

    public const string InstructionsSheet = "Instructions";
    public const string ContactsSheet = "Contacts";
    public const string AccountLinksSheet = "AccountLinks";
    public const string ReferenceDataSheet = "ReferenceData";
    public const string AccountsSheet = "Accounts";

    /// <summary>Bumped whenever a column is added/removed/renamed; written into Instructions so the Task 2 reader can
    /// reject a stale file instead of mis-mapping columns.</summary>
    public const string TemplateVersion = "1.0";

    // ---- Contacts sheet ----

    public const string OperationColumn = "Operation";
    public const string ContactIdColumn = "ContactId";
    public const string LinkIdColumn = "LinkId";

    public static readonly IReadOnlyList<string> ContactColumns = new[]
    {
        OperationColumn, ContactIdColumn, "ExternalSystem", "ExternalId", "FirstName", "LastName", "DisplayName",
        "ContactType", "ContactStatus", "Gender", "ProfessionalTitle", "Specialty", "Department",
        "CountryCode", "CityCode", "DistrictCode", "AddressLine", "PostalCode", "PreferredLanguage",
        "PhoneCountryCode", "Phone", "Email", "Notes"
    };

    // ---- AccountLinks sheet ----

    public static readonly IReadOnlyList<string> AccountLinkColumns = new[]
    {
        OperationColumn, LinkIdColumn, ContactIdColumn, "ContactExternalSystem", "ContactExternalId",
        "AccountId", "AccountCode", "AccountName", "RoleCode", "IsPrimary", "Status",
        "ValidFrom", "ValidTo", "ReportsToContactId", "Notes", "CrossCountryReason"
    };

    // ---- Helper sheets ----

    public static readonly IReadOnlyList<string> ReferenceDataColumns = new[]
    {
        "SetCode", "ValueCode", "DisplayName", "Description", "IsActive", "IsDeprecated", "Metadata"
    };

    public static readonly IReadOnlyList<string> AccountColumns = new[]
    {
        "AccountId", "AccountCode", "AccountName", "AccountType", "CountryCode", "CityCode"
    };

    // ---- MOD-0048 set codes -------------------------------------------------------------------------------------
    // Reused from the single-write validation path wherever it already declares them (ContactReferenceValidation), so
    // template/export/validation can never drift apart. The two codes the backend had no constant for yet
    // (preferred-language / phone-country-code) mirror the frontend Contact form's set codes — existing MOD-0048 sets,
    // NOT new ones.

    public const string ContactRoleSet = "contact-role";
    public const string PreferredLanguageSet = "preferred-language";
    public const string PhoneCountryCodeSet = "phone-country-code";

    /// <summary>Sets whose absence blocks a (future) import — surfaced as a warning on the Instructions sheet.</summary>
    public static readonly IReadOnlyList<string> RequiredSets = new[]
    {
        ContactReferenceValidation.ContactTypeSet,
        ContactReferenceValidation.ContactStatusSet,
        ContactRoleSet
    };

    /// <summary>Optional sets — an unpublished one is reported as NOT_PUBLISHED and simply leaves its dropdown empty.</summary>
    public static readonly IReadOnlyList<string> OptionalSets = new[]
    {
        ContactReferenceValidation.GenderSet,
        ContactReferenceValidation.ProfessionalTitleSet,
        ContactReferenceValidation.MedicalSpecialtySet,
        ContactReferenceValidation.DepartmentTypeSet,
        ContactReferenceValidation.CountrySet,
        ContactReferenceValidation.CitySet,
        ContactReferenceValidation.DistrictSet,
        PreferredLanguageSet,
        PhoneCountryCodeSet
    };

    public static IReadOnlyList<string> AllSets => RequiredSets.Concat(OptionalSets).ToList();

    /// <summary>Contacts sheet column → MOD-0048 set code (drives the in-cell dropdown).</summary>
    public static readonly IReadOnlyDictionary<string, string> ContactColumnSets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ContactType"] = ContactReferenceValidation.ContactTypeSet,
        ["ContactStatus"] = ContactReferenceValidation.ContactStatusSet,
        ["Gender"] = ContactReferenceValidation.GenderSet,
        ["ProfessionalTitle"] = ContactReferenceValidation.ProfessionalTitleSet,
        ["Specialty"] = ContactReferenceValidation.MedicalSpecialtySet,
        ["Department"] = ContactReferenceValidation.DepartmentTypeSet,
        ["CountryCode"] = ContactReferenceValidation.CountrySet,
        ["CityCode"] = ContactReferenceValidation.CitySet,
        ["DistrictCode"] = ContactReferenceValidation.DistrictSet,
        ["PreferredLanguage"] = PreferredLanguageSet,
        ["PhoneCountryCode"] = PhoneCountryCodeSet
    };

    /// <summary>AccountLinks sheet column → MOD-0048 set code. <c>Status</c> is intentionally absent: there is no
    /// <c>account-contact-link-status</c> published set, and inventing one is forbidden — it stays a free internal
    /// lifecycle marker (see AccountContactLink entity).</summary>
    public static readonly IReadOnlyDictionary<string, string> AccountLinkColumnSets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["RoleCode"] = ContactRoleSet
    };

    /// <summary>Columns forced to Excel text format so leading zeros / "+" prefixes survive (postal codes, dial codes,
    /// external ids that look numeric).</summary>
    public static readonly IReadOnlyList<string> TextFormattedColumns = new[]
    {
        "ExternalId", "PostalCode", "PhoneCountryCode", "Phone", "ContactExternalId"
    };

    /// <summary>System-owned identity columns: written by export, must not be edited by hand.</summary>
    public static readonly IReadOnlyList<string> SystemColumns = new[]
    {
        ContactIdColumn, LinkIdColumn, "AccountId", "ReportsToContactId"
    };

    /// <summary>Read-only helper columns: exported for human readability, ignored by the (future) import reader.</summary>
    public static readonly IReadOnlyList<string> ReadOnlyHelperColumns = new[] { "AccountName" };

    /// <summary>Allowed <c>Operation</c> values. Task 1 writes them into the template only — no import executes them yet.</summary>
    public static readonly IReadOnlyList<string> OperationValues = new[] { "add", "update", "end", "skip" };

    public const string NotPublishedMarker = "NOT_PUBLISHED";

    // ---- Export guard rails ----

    /// <summary>Safe default row ceilings for a synchronous, request-scoped export. Exceeding one returns a controlled
    /// 400 asking the caller to filter, instead of streaming an unbounded PII payload / timing out.</summary>
    public const int MaxContactRows = 5000;

    public const int MaxLinkRows = 20000;

    public const int MaxAccountLookupRows = 2000;

    public static int ColumnIndex(IReadOnlyList<string> columns, string column)
        => columns.ToList().FindIndex(c => string.Equals(c, column, StringComparison.OrdinalIgnoreCase)) + 1;
}
