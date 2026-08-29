using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.AccountContact.Handlers;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.ImportExport;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Catalog;

/// <summary>
/// MOD-0167 FU02 (D5) — the <b>closed</b> attribute catalog. Every attribute usable in a criteria predicate is declared
/// here as a code, with its evaluation class, value type, allowed operators, required/optional parameters and the
/// subject types it applies to. An <c>AttributeCode</c> that is not declared is a 400
/// (<see cref="SegmentErrorCodes.AttributeUnknown"/>) at authoring time.
/// <para>Why closed: a free field name makes a rule <i>silently</i> breakable — rename the underlying field and the
/// criterion matches nothing while reporting no error. A declared catalog (a) rejects at create, (b) feeds the UI with
/// no hardcoded list, and (c) makes visible which attribute crosses a process boundary, so the fail-closed behaviour is
/// predictable.</para>
/// <para>Deliberately ABSENT: visit.* / last-visit (MOD-0155), frequency.* and campaign.* (MOD-0165), journey.*
/// (MOD-0166), knowledge.content.* (MOD-0162 FU02 — concept.affinity reads CONCEPTS, not content), rep.* / person.*
/// (Rep = User; person master is MOD-0288), score.* / rfm.* / icp.* (FU-D) and segment.* (segment inside a segment is a
/// cycle risk — FU-B).</para>
/// </summary>
public static class SegmentAttributeCatalog
{
    /// <summary>Native: pushed down into the single Phase-1 Mongo filter (same collection).</summary>
    public const string ClassNative = "N";

    /// <summary>In-service join: candidates are narrowed through AccountContactLink in ONE bulk read (Phase 1.5).</summary>
    public const string ClassJoin = "J";

    /// <summary>Derived in-service: post-filtered over the candidate set with ONE bulk read per source (Phase 2).
    /// Uncertainty here is an ANSWER (candidate eliminated + reason code), never a 503.</summary>
    public const string ClassDerived = "D";

    /// <summary>Cross-service: HTTP over the Gateway, used ONLY to validate the criterion VALUE. It never derives
    /// membership. Uncertainty here is a 503 with no partial result and no persistence.</summary>
    public const string ClassCrossService = "X";

    // Cross-service reference kinds (which MDM master a value is proven against).
    public const string ReferenceKindGlobalProduct = "global-product";
    public const string ReferenceKindProduct = "product";
    public const string ReferenceKindBrand = "brand";

    // --- attribute codes -------------------------------------------------------------------------------------
    public const string AccountType = "account.type";
    public const string AccountCategory = "account.category";
    public const string AccountStatus = "account.status";
    public const string AccountCountry = "account.country";
    public const string AccountCity = "account.city";
    public const string AccountDistrict = "account.district";
    public const string AccountParentAccount = "account.parent-account";
    public const string AccountCreatedAt = "account.created-at";
    public const string AccountAttribute = "account.attribute";

    public const string ContactType = "contact.type";
    public const string ContactStatus = "contact.status";
    public const string ContactGender = "contact.gender";
    public const string ContactSpecialty = "contact.specialty";
    public const string ContactProfessionalTitle = "contact.professional-title";
    public const string ContactDepartment = "contact.department";
    public const string ContactCountry = "contact.country";
    public const string ContactCity = "contact.city";
    public const string ContactDistrict = "contact.district";
    public const string ContactPreferredLanguage = "contact.preferred-language";
    public const string ContactCreatedAt = "contact.created-at";

    public const string ContactAccountRole = "contact.account-role";
    public const string ContactIsPrimary = "contact.is-primary";
    public const string ContactAccountType = "contact.account-type";

    public const string TerritoryHasCoverage = "territory.has-coverage";
    public const string TerritoryNode = "territory.node";
    public const string TerritoryModel = "territory.model";

    public const string ConsentEligibility = "consent.eligibility";
    public const string ConsentScopeProduct = "consent.scope-product";
    public const string ConsentScopeBrand = "consent.scope-brand";

    /// <summary>The ConceptGraph-derived product affinity (D-PRODUCT). Class D: the graph lives in THIS service, so an
    /// empty graph is an empty answer, not a dependency failure. The VALUE is still proven cross-service against the
    /// MDM global product.</summary>
    public const string ConceptAffinity = "concept.affinity";

    // --- parameter keys --------------------------------------------------------------------------------------
    public const string ParameterAttributeCode = "attributeCode";
    public const string ParameterChannel = "channel";
    public const string ParameterPurpose = "purpose";
    public const string ParameterMaxDepth = "maxDepth";
    public const string ParameterSubjectId = "subjectId";

    private static readonly string[] BothSubjects = { SegmentSubjectTypes.Account, SegmentSubjectTypes.Contact };
    private static readonly string[] AccountOnly = { SegmentSubjectTypes.Account };
    private static readonly string[] ContactOnly = { SegmentSubjectTypes.Contact };

    private static readonly string[] EqualityOps =
        { SegmentOperators.Eq, SegmentOperators.Ne, SegmentOperators.In, SegmentOperators.NotIn };

    private static readonly string[] EqualityWithNullOps =
    {
        SegmentOperators.Eq, SegmentOperators.Ne, SegmentOperators.In, SegmentOperators.NotIn,
        SegmentOperators.IsNull, SegmentOperators.IsNotNull
    };

    private static readonly string[] TextOps =
    {
        SegmentOperators.Eq, SegmentOperators.Ne, SegmentOperators.In, SegmentOperators.NotIn,
        SegmentOperators.Contains, SegmentOperators.IsNull, SegmentOperators.IsNotNull
    };

    private static readonly string[] DateOps =
    {
        SegmentOperators.Gt, SegmentOperators.Gte, SegmentOperators.Lt, SegmentOperators.Lte,
        SegmentOperators.Between
    };

    private static readonly string[] GuidRefOps = { SegmentOperators.Eq, SegmentOperators.In };
    private static readonly string[] MembershipOps = { SegmentOperators.Eq, SegmentOperators.In };

    // ---- P1a value sources -------------------------------------------------------------------------------------
    // Every reference set below is READ FROM THE EXISTING WRITE-PATH VALIDATOR, never re-declared, so the criteria
    // editor can only ever offer a value the Account / Contact / link write path would also accept. If one of those
    // validators changes its set, this catalog follows automatically.
    private static readonly SegmentAttributeValueSource AccountTypeValues =
        SegmentAttributeValueSource.ReferenceSet(AccountReferenceValidation.AccountTypeSet);
    private static readonly SegmentAttributeValueSource AccountCategoryValues =
        SegmentAttributeValueSource.ReferenceSet(AccountReferenceValidation.AccountCategorySet);
    private static readonly SegmentAttributeValueSource AccountStatusValues =
        SegmentAttributeValueSource.ReferenceSet(AccountReferenceValidation.AccountStatusSet);
    // Location sets are shared between Account and Contact (ContactReferenceValidation says so in its own comment).
    private static readonly SegmentAttributeValueSource CountryValues =
        SegmentAttributeValueSource.ReferenceSet(ContactReferenceValidation.CountrySet);
    private static readonly SegmentAttributeValueSource CityValues =
        SegmentAttributeValueSource.ReferenceSet(ContactReferenceValidation.CitySet);
    private static readonly SegmentAttributeValueSource DistrictValues =
        SegmentAttributeValueSource.ReferenceSet(ContactReferenceValidation.DistrictSet);
    private static readonly SegmentAttributeValueSource ContactTypeValues =
        SegmentAttributeValueSource.ReferenceSet(ContactReferenceValidation.ContactTypeSet);
    private static readonly SegmentAttributeValueSource ContactStatusValues =
        SegmentAttributeValueSource.ReferenceSet(ContactReferenceValidation.ContactStatusSet);
    private static readonly SegmentAttributeValueSource GenderValues =
        SegmentAttributeValueSource.ReferenceSet(ContactReferenceValidation.GenderSet);
    private static readonly SegmentAttributeValueSource SpecialtyValues =
        SegmentAttributeValueSource.ReferenceSet(ContactReferenceValidation.MedicalSpecialtySet);
    private static readonly SegmentAttributeValueSource ProfessionalTitleValues =
        SegmentAttributeValueSource.ReferenceSet(ContactReferenceValidation.ProfessionalTitleSet);
    private static readonly SegmentAttributeValueSource DepartmentValues =
        SegmentAttributeValueSource.ReferenceSet(ContactReferenceValidation.DepartmentTypeSet);
    private static readonly SegmentAttributeValueSource PreferredLanguageValues =
        SegmentAttributeValueSource.ReferenceSet(ContactWorkbookSchema.PreferredLanguageSet);
    private static readonly SegmentAttributeValueSource ContactRoleValues =
        SegmentAttributeValueSource.ReferenceSet(AccountContactValidation.ContactRoleSet);

    /// <summary>The consent verdict is a closed in-domain vocabulary, so it rides on the catalog itself rather than
    /// costing the UI a second call. Taken from the MOD-0164 constants so the two can never disagree.</summary>
    private static readonly SegmentAttributeValueSource ConsentEligibilityValues =
        SegmentAttributeValueSource.Enum(
            ConsentEligibilityStatus.Allowed,
            ConsentEligibilityStatus.Blocked,
            ConsentEligibilityStatus.Unknown,
            ConsentEligibilityStatus.NotApplicable);

    private static readonly IReadOnlyList<SegmentAttributeDefinition> Definitions = new List<SegmentAttributeDefinition>
    {
        // ---- N: native Account attributes (Phase-1 pushdown) ----
        Native(AccountType, "Account.AccountType (MOD-0149)", SegmentValueTypes.String, EqualityWithNullOps, AccountOnly,
            valueSource: AccountTypeValues),
        Native(AccountCategory, "Account.AccountCategory (MOD-0149)", SegmentValueTypes.String, EqualityWithNullOps, AccountOnly,
            valueSource: AccountCategoryValues),
        Native(AccountStatus, "Account.Status (MOD-0149)", SegmentValueTypes.String, EqualityWithNullOps, AccountOnly,
            valueSource: AccountStatusValues),
        Native(AccountCountry, "Account.CountryRef (MOD-0149)", SegmentValueTypes.String, EqualityOps, AccountOnly,
            valueSource: CountryValues),
        Native(AccountCity, "Account.CityRef (MOD-0149)", SegmentValueTypes.String, EqualityOps, AccountOnly,
            valueSource: CityValues),
        Native(AccountDistrict, "Account.DistrictRef (MOD-0149)", SegmentValueTypes.String, EqualityOps, AccountOnly,
            valueSource: DistrictValues),
        Native(AccountParentAccount, "Account.ParentAccountId (MOD-0149)", SegmentValueTypes.Guid,
            new[] { SegmentOperators.Eq, SegmentOperators.Ne, SegmentOperators.IsNull, SegmentOperators.IsNotNull },
            AccountOnly,
            valueSource: SegmentAttributeValueSource.EntityPicker(SegmentAttributeValueSource.EntityAccount)),
        // A date needs a picker, not a value list: the ValueType already tells the UI that.
        Native(AccountCreatedAt, "Account.CreatedAt (MOD-0149)", SegmentValueTypes.Date, DateOps, AccountOnly),
        // Deliberately FREE TEXT: the key is tenant-authored and so is the value. There is no MOD-0048 set behind an
        // AccountAttributeValue, and inventing one here would be a second source of truth (see F-TIER).
        Native(AccountAttribute, "AccountAttributeValue (MOD-0149)", SegmentValueTypes.String, TextOps, AccountOnly,
            requiredParameters: new[] { ParameterAttributeCode }),

        // ---- N: native Contact attributes ----
        Native(ContactType, "Contact.ContactType (MOD-0150)", SegmentValueTypes.String, EqualityOps, ContactOnly,
            valueSource: ContactTypeValues),
        Native(ContactStatus, "Contact.Status (MOD-0150)", SegmentValueTypes.String, EqualityOps, ContactOnly,
            valueSource: ContactStatusValues),
        Native(ContactGender, "Contact.Gender (MOD-0150)", SegmentValueTypes.String, EqualityOps, ContactOnly,
            valueSource: GenderValues),
        // The same set the concept graph specialty nodes must come from (F-CONCEPT-DATA): offering it here is what
        // keeps contact.specialty and concept.affinity talking about the same codes.
        Native(ContactSpecialty, "Contact.Specialty (MOD-0150)", SegmentValueTypes.String, TextOps, ContactOnly,
            valueSource: SpecialtyValues),
        Native(ContactProfessionalTitle, "Contact.ProfessionalTitle (MOD-0150)", SegmentValueTypes.String, TextOps, ContactOnly,
            valueSource: ProfessionalTitleValues),
        Native(ContactDepartment, "Contact.Department (MOD-0150)", SegmentValueTypes.String, TextOps, ContactOnly,
            valueSource: DepartmentValues),
        Native(ContactCountry, "Contact.CountryRef (MOD-0150)", SegmentValueTypes.String, EqualityOps, ContactOnly,
            valueSource: CountryValues),
        Native(ContactCity, "Contact.CityRef (MOD-0150)", SegmentValueTypes.String, EqualityOps, ContactOnly,
            valueSource: CityValues),
        Native(ContactDistrict, "Contact.DistrictRef (MOD-0150)", SegmentValueTypes.String, EqualityOps, ContactOnly,
            valueSource: DistrictValues),
        Native(ContactPreferredLanguage, "Contact.PreferredLanguage (MOD-0150)", SegmentValueTypes.String, EqualityOps, ContactOnly,
            valueSource: PreferredLanguageValues),
        Native(ContactCreatedAt, "Contact.CreatedAt (MOD-0150)", SegmentValueTypes.Date, DateOps, ContactOnly),

        // ---- J: in-service join through AccountContactLink (one bulk read) ----
        Join(ContactAccountRole, "AccountContactLink.RoleCode, active link (MOD-0150)", SegmentValueTypes.String,
            MembershipOps, ContactOnly, valueSource: ContactRoleValues),
        // Bool: the ValueType is the whole instruction the UI needs.
        Join(ContactIsPrimary, "AccountContactLink.IsPrimary, active link (MOD-0150)", SegmentValueTypes.Bool,
            new[] { SegmentOperators.Eq }, ContactOnly),
        Join(ContactAccountType, "linked Account.AccountType (MOD-0149/0150)", SegmentValueTypes.String,
            MembershipOps, ContactOnly, valueSource: AccountTypeValues),

        // ---- D: derived in-service (one bulk read per source; uncertainty eliminates, never 503) ----
        Derived(TerritoryHasCoverage, "MOD-0151 AccountCurrentCoverageResolver", SegmentValueTypes.Bool,
            new[] { SegmentOperators.Eq }, BothSubjects),
        Derived(TerritoryNode, "MOD-0151 current coverage (TerritoryNodeId)", SegmentValueTypes.Guid,
            GuidRefOps, BothSubjects,
            valueSource: SegmentAttributeValueSource.EntityPicker(SegmentAttributeValueSource.EntityTerritoryNode)),
        Derived(TerritoryModel, "MOD-0151 current coverage (TerritoryModelId)", SegmentValueTypes.Guid,
            GuidRefOps, BothSubjects,
            valueSource: SegmentAttributeValueSource.EntityPicker(SegmentAttributeValueSource.EntityTerritoryModel)),
        Derived(ConsentEligibility, "MOD-0164 consent/preference evaluation (allowed|blocked|unknown|not_applicable)",
            SegmentValueTypes.String, MembershipOps, BothSubjects,
            requiredParameters: new[] { ParameterChannel, ParameterPurpose },
            valueSource: ConsentEligibilityValues),
        Derived(ConsentScopeProduct, "MOD-0164 consent scope (product); the VALUE is proven in MDM (fail-closed)",
            SegmentValueTypes.Guid, GuidRefOps, BothSubjects,
            referenceKind: ReferenceKindProduct,
            valueSource: SegmentAttributeValueSource.EntityPicker(SegmentAttributeValueSource.EntityMdmProduct)),
        Derived(ConsentScopeBrand, "MOD-0164 consent scope (brand); the VALUE is proven in MDM (fail-closed)",
            SegmentValueTypes.Guid, GuidRefOps, BothSubjects,
            referenceKind: ReferenceKindBrand,
            valueSource: SegmentAttributeValueSource.EntityPicker(SegmentAttributeValueSource.EntityMdmBrand)),

        // ---- D: ConceptGraph-derived product affinity (D-PRODUCT). READ-ONLY consumption of MOD-0162 FU03. ----
        Derived(ConceptAffinity,
            "MOD-0162 FU03 ConceptGraph (READ-ONLY): global-product node, bounded addresses/belongs-to traversal, "
            + "reference-data-value specialty nodes, matched against the candidate contact.specialty. The VALUE is "
            + "proven in MDM (fail-closed); an empty graph is an EMPTY ANSWER with a reason code, never a 503.",
            SegmentValueTypes.Guid, GuidRefOps, ContactOnly,
            optionalParameters: new[] { ParameterMaxDepth, ParameterSubjectId },
            referenceKind: ReferenceKindGlobalProduct,
            valueSource: SegmentAttributeValueSource.EntityPicker(SegmentAttributeValueSource.EntityGlobalProduct))
    };

    private static readonly Dictionary<string, SegmentAttributeDefinition> ByCode =
        Definitions.ToDictionary(d => d.AttributeCode, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<SegmentAttributeDefinition> All => Definitions;

    public static SegmentAttributeDefinition? Find(string? attributeCode)
        => string.IsNullOrWhiteSpace(attributeCode)
            ? null
            : ByCode.GetValueOrDefault(attributeCode.Trim().ToLowerInvariant());

    public static bool IsDeclared(string? attributeCode) => Find(attributeCode) is not null;

    private static SegmentAttributeDefinition Native(
        string code, string source, string valueType, IReadOnlyList<string> operators,
        IReadOnlyList<string> subjectTypes, IReadOnlyList<string>? requiredParameters = null,
        SegmentAttributeValueSource? valueSource = null)
        => new(code, ClassNative, source, valueType, operators, requiredParameters ?? Array.Empty<string>(),
            Array.Empty<string>(), subjectTypes, null, valueSource ?? SegmentAttributeValueSource.FreeText);

    private static SegmentAttributeDefinition Join(
        string code, string source, string valueType, IReadOnlyList<string> operators,
        IReadOnlyList<string> subjectTypes, SegmentAttributeValueSource? valueSource = null)
        => new(code, ClassJoin, source, valueType, operators, Array.Empty<string>(), Array.Empty<string>(),
            subjectTypes, null, valueSource ?? SegmentAttributeValueSource.FreeText);

    private static SegmentAttributeDefinition Derived(
        string code, string source, string valueType, IReadOnlyList<string> operators,
        IReadOnlyList<string> subjectTypes, IReadOnlyList<string>? requiredParameters = null,
        IReadOnlyList<string>? optionalParameters = null, string? referenceKind = null,
        SegmentAttributeValueSource? valueSource = null)
        => new(code, ClassDerived, source, valueType, operators, requiredParameters ?? Array.Empty<string>(),
            optionalParameters ?? Array.Empty<string>(), subjectTypes, referenceKind,
            valueSource ?? SegmentAttributeValueSource.FreeText);
}
