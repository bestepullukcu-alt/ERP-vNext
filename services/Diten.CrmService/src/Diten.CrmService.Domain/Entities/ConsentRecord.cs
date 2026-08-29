namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0164 FU02 — Consent record. Answers <b>one</b> question: "is this subject permitted for this channel and this
/// purpose, within this scope, at this instant?" It deliberately does NOT answer "when is the subject available?"
/// (MOD-0150), "how often should it be visited?" (MOD-0165), "which route/order?" (MOD-0155), "what to show?"
/// (MOD-0162) or "who is in the segment?" (MOD-0167).
/// <para>
/// This is its OWN aggregate (SoR = MOD-0164). Consent is never a flat field on Contact / AccountContactLink /
/// Account — a subject holds many consents over time, per channel, per purpose and per scope, so a flat field would
/// collapse provenance, legal basis and effective windows into one wrong value, and would let a channel/purpose
/// permission leak into another. There is <b>no general consent flag</b>: consent is always evaluated as
/// <c>subject × channel × purpose × scope × time</c>.
/// </para>
/// <para>
/// <see cref="EntityBase.Id"/> is the ConsentId. Withdrawal is a <see cref="ConsentStatus"/> transition (audit
/// stamped) — it never deletes the record. Closing a record is the soft <see cref="ArchivedAt"/> lifecycle; there is
/// no hard delete. The question dimensions (SubjectType/SubjectId/Channel/Purpose/ScopeType/ScopeId) are immutable
/// after create, so a record can never be silently repurposed to answer a different question.
/// </para>
/// </summary>
public sealed class ConsentRecord : EntityBase
{
    /// <summary><see cref="ConsentSubjectType"/> — what kind of subject this consent belongs to. Immutable.</summary>
    public string SubjectType { get; set; } = string.Empty;

    /// <summary>Identity of the subject within <see cref="SubjectType"/>. Never empty. Immutable. The subject master
    /// is NOT read or mutated here — the caller supplies the id.</summary>
    public Guid SubjectId { get; set; }

    /// <summary><see cref="ConsentScopeType"/> — optional narrowing context (brand, product, campaign, …). Absent
    /// means the consent is general for its channel+purpose. Immutable.</summary>
    public string? ScopeType { get; set; }

    /// <summary>Identity within <see cref="ScopeType"/>. May be null while <see cref="ScopeType"/> is set ("any
    /// instance of this scope kind"). Immutable.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary><see cref="ConsentChannel"/> — the communication/interaction channel the permission covers. A channel
    /// permission is NEVER transferable to another channel. Immutable.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary><see cref="ConsentPurpose"/> — why the subject may be contacted. A purpose permission is NEVER
    /// transferable to another purpose. Immutable.</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary><see cref="ConsentLegalBasis"/> — lawful basis tag. Required; this runtime records it, it does not
    /// interpret it legally.</summary>
    public string LegalBasis { get; set; } = string.Empty;

    /// <summary><see cref="ConsentStatuses"/> — granted / denied / withdrawn / restricted / unknown / expired.
    /// Required (never defaulted): an absent state is authored as <c>unknown</c>, and <c>unknown</c> is never
    /// evaluated as allowed.</summary>
    public string ConsentStatus { get; set; } = string.Empty;

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Open-ended when null. A record whose window has closed is never allowed (it is reported as expired).</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary><see cref="ConsentSource"/> — provenance of the record. Audit visible.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Optional pointer to the MOD-0028/MOD-0029 document or file that evidences this consent. A REFERENCE
    /// only — no file is uploaded, rendered or copied here.</summary>
    public ConsentEvidenceRef? EvidenceRef { get; set; }

    /// <summary>Required when <see cref="ConsentStatus"/> is <c>withdrawn</c>; preserved forever afterwards.</summary>
    public string? WithdrawalReason { get; set; }

    public string? Notes { get; set; }

    /// <summary>Legacy / external-system identities. Silent merge is forbidden: a duplicate
    /// (SourceSystem, ExternalId) mapping is a reported conflict, never an overwrite.</summary>
    public List<ConsentExternalReference> ExternalReferences { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Soft lifecycle close. An archived record stays readable as history but is excluded from evaluation.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    /// <summary>Effective at a given instant: EffectiveFrom ≤ at ≤ EffectiveTo (open end when EffectiveTo is null).</summary>
    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);

    /// <summary>The window has closed before <paramref name="at"/> — the record is expired, never allowed.</summary>
    public bool HasExpiredAt(DateTimeOffset at) => EffectiveTo is { } to && to < at;

    /// <summary>The window has not opened yet at <paramref name="at"/>.</summary>
    public bool IsNotYetEffectiveAt(DateTimeOffset at) => at < EffectiveFrom;
}

/// <summary>
/// MOD-0164 FU02 — Preference record. Answers "which channel / restriction / preference does this subject express?".
/// A preference NEVER substitutes for consent: it can only <b>restrict further</b>, never grant. Its own aggregate
/// (SoR = MOD-0164); never a flat field on a subject master.
/// <para>
/// Distinct from MOD-0150 ContactAvailability: availability = <i>when</i> is the subject available; preference =
/// <i>which channel / restriction / preference</i>. Neither replaces the other.
/// </para>
/// </summary>
public sealed class PreferenceRecord : EntityBase
{
    /// <summary><see cref="ConsentSubjectType"/> — same subject vocabulary as consent. Immutable.</summary>
    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    /// <summary><see cref="PreferenceChannel"/> — a consent channel, or the <c>all</c> sentinel meaning the
    /// preference applies to every channel (so a blanket do-not-contact needs one record, not nine). Immutable.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary><see cref="PreferenceType"/> — what kind of preference/restriction this is. Immutable.</summary>
    public string PreferenceType { get; set; } = string.Empty;

    /// <summary>The authored value. For the restrictive boolean types (<c>do-not-contact</c>, <c>do-not-visit</c>)
    /// this must be a boolean literal, and only <c>true</c> restricts — a <c>false</c> record never blocks and is
    /// never read as an opt-in for consent.</summary>
    public string PreferenceValue { get; set; } = string.Empty;

    /// <summary>Deterministic tie-break weight. Smaller wins. Required (≥ 1), never auto-defaulted.</summary>
    public int Priority { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary><see cref="ConsentSource"/> — same provenance vocabulary as consent.</summary>
    public string Source { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public List<ConsentExternalReference> ExternalReferences { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);

    /// <summary>True when this preference applies to <paramref name="channel"/> — either an exact channel match or
    /// the <c>all</c> sentinel. A preference authored for one channel never leaks into another.</summary>
    public bool AppliesToChannel(string channel)
        => string.Equals(Channel, PreferenceChannel.AnyChannel, StringComparison.OrdinalIgnoreCase)
           || string.Equals(Channel, channel?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the authored <see cref="PreferenceValue"/> is the boolean <c>true</c>. Only meaningful for the
    /// restrictive boolean types; anything unparseable is NOT treated as true (a malformed value never invents a
    /// restriction, and validation rejects it on write).</summary>
    public bool IsRestrictiveValueTrue()
        => bool.TryParse(PreferenceValue?.Trim(), out var parsed) && parsed;
}

/// <summary>
/// A MOD-0028/MOD-0029 document/file pointer that evidences a consent record. Reference ONLY: MOD-0164 never uploads,
/// renders, copies or packages the file, and it does not resolve the reference against the document master in FU02
/// (format-level validation only — see the FU02 report).
/// </summary>
public sealed class ConsentEvidenceRef
{
    /// <summary><see cref="ConsentEvidenceRefType"/> — document or file.</summary>
    public string RefType { get; set; } = string.Empty;

    /// <summary>Identity in the owning document module. Never empty.</summary>
    public Guid RefId { get; set; }

    /// <summary>Owning module: MOD-0028 (files) or MOD-0029 (controlled documents).</summary>
    public string SourceModule { get; set; } = string.Empty;

    /// <summary>Optional human-readable code/number carried for audit display only.</summary>
    public string? RefCode { get; set; }
}

/// <summary>Kind of evidence pointer. Structural, in-domain.</summary>
public static class ConsentEvidenceRefType
{
    public const string Document = "document";
    public const string File = "file";

    public static readonly IReadOnlyList<string> All = new[] { Document, File };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Owning module of an <see cref="ConsentEvidenceRef"/>. MOD-0164 stores the pointer only — the file SoR
/// stays MOD-0028 (files) / MOD-0029 (controlled documents) and nothing is copied here.</summary>
public static class ConsentEvidenceSourceModule
{
    public const string Files = "MOD-0028";
    public const string ControlledDocuments = "MOD-0029";

    public static readonly IReadOnlyList<string> All = new[] { Files, ControlledDocuments };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && All.Contains(value.Trim().ToUpperInvariant());

    public static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;
}

/// <summary>External/legacy identity carried by a consent or preference record. Same contract as MOD-0290-FU01 /
/// MOD-0165-FU02 (SourceSystem · ExternalId · ExternalCode · ExternalName · ImportedAt · IsPrimary).</summary>
public sealed class ConsentExternalReference
{
    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalCode { get; set; }
    public string? ExternalName { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>What a consent/preference record can belong to. Structural vocabulary (not tenant vocabulary), so it is
/// validated in-domain — the same way the MOD-0165 frequency vocabulary is — and never fails open on an unpublished
/// MOD-0048 set.</summary>
public static class ConsentSubjectType
{
    public const string Contact = "contact";
    public const string AccountContactLink = "account-contact-link";
    public const string Account = "account";
    public const string AudienceProfile = "audience-profile";
    public const string CampaignTarget = "campaign-target";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Contact, AccountContactLink, Account, AudienceProfile, CampaignTarget
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Channel a consent permission covers. In-domain (structural).</summary>
public static class ConsentChannel
{
    public const string Visit = "visit";
    public const string Email = "email";
    public const string Sms = "sms";
    public const string Phone = "phone";
    public const string WhatsApp = "whatsapp";
    public const string Portal = "portal";
    public const string DigitalDetailing = "digital-detailing";
    public const string Training = "training";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Visit, Email, Sms, Phone, WhatsApp, Portal, DigitalDetailing, Training, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Preference channel vocabulary = the consent channels plus the <c>all</c> sentinel. The sentinel exists
/// ONLY for preferences (a blanket restriction is one record); consent never uses it, because a channel permission is
/// never transferable.</summary>
public static class PreferenceChannel
{
    /// <summary>The "every channel" sentinel (wire value <c>all</c>). Preferences only.</summary>
    public const string AnyChannel = "all";

    public static readonly IReadOnlyList<string> All =
        ConsentChannel.All.Concat(new[] { AnyChannel }).ToList();

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Why the subject may be contacted. In-domain (structural).</summary>
public static class ConsentPurpose
{
    public const string Campaign = "campaign";
    public const string MedicalVisit = "medical-visit";
    public const string ProductInformation = "product-information";
    public const string Training = "training";
    public const string Marketing = "marketing";
    public const string Service = "service";
    public const string Compliance = "compliance";
    public const string Research = "research";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Campaign, MedicalVisit, ProductInformation, Training, Marketing, Service, Compliance, Research, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Lawful basis tag. Recorded, not legally interpreted, by this runtime.</summary>
public static class ConsentLegalBasis
{
    public const string ExplicitConsent = "explicit-consent";
    public const string Contract = "contract";
    public const string LegalObligation = "legal-obligation";
    public const string LegitimateInterest = "legitimate-interest";
    public const string PublicInterest = "public-interest";
    public const string VitalInterest = "vital-interest";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ExplicitConsent, Contract, LegalObligation, LegitimateInterest, PublicInterest, VitalInterest, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>
/// Consent state vocabulary. NOT a lifecycle: archiving is <see cref="ConsentRecord.ArchivedAt"/>, and there is no
/// hard delete. <c>unknown</c> is an explicit authored state and is NEVER evaluated as allowed; <c>expired</c> may be
/// authored explicitly and is also derived from a closed effective window — either way it is never allowed.
/// </summary>
public static class ConsentStatuses
{
    public const string Granted = "granted";
    public const string Denied = "denied";
    public const string Withdrawn = "withdrawn";
    public const string Restricted = "restricted";
    public const string Unknown = "unknown";
    public const string Expired = "expired";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Granted, Denied, Withdrawn, Restricted, Unknown, Expired
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>Statuses that block. A restrictive record beats a granted record at the same scope specificity.</summary>
    public static bool IsRestrictive(string? value) => Normalize(value) switch
    {
        Denied or Withdrawn or Restricted => true,
        _ => false
    };

    /// <summary>
    /// Fail-closed status precedence — <b>smaller wins</b>: restrictive (denied/withdrawn/restricted) &gt; granted &gt;
    /// unknown. <c>expired</c> never competes (it is eliminated before ordering) and ranks last defensively.
    /// </summary>
    public static int Precedence(string? value) => Normalize(value) switch
    {
        Denied or Withdrawn or Restricted => 1,
        Granted => 2,
        Unknown => 3,
        _ => 4
    };
}

/// <summary>Optional narrowing context of a consent. In-domain (structural). Ids are supplied by the caller; no
/// brand/product/campaign/segment master is read or opened here.</summary>
public static class ConsentScopeType
{
    public const string Brand = "brand";
    public const string Product = "product";
    public const string Campaign = "campaign";
    public const string Segment = "segment";
    public const string TherapeuticArea = "therapeutic-area";
    public const string KnowledgeContent = "knowledge-content";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Brand, Product, Campaign, Segment, TherapeuticArea, KnowledgeContent, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>Scope specificity for the resolution tie-break — <b>smaller = more specific = wins</b>. A record
    /// pinned to one scope instance beats a scope-kind record, which beats a general record.</summary>
    public static int Specificity(string? scopeType, Guid? scopeId)
    {
        if (!string.IsNullOrWhiteSpace(scopeType) && scopeId is { } id && id != Guid.Empty)
        {
            return 1;
        }

        return string.IsNullOrWhiteSpace(scopeType) ? 3 : 2;
    }
}

/// <summary>Provenance of a consent/preference record. Audit visible. In-domain (structural).</summary>
public static class ConsentSource
{
    public const string SubjectDeclared = "subject-declared";
    public const string FieldCapture = "field-capture";
    public const string Portal = "portal";
    public const string ConsentCenter = "consent-center";
    public const string LegacyImport = "legacy-import";
    public const string ContractDocument = "contract-document";
    public const string Manual = "manual";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SubjectDeclared, FieldCapture, Portal, ConsentCenter, LegacyImport, ContractDocument, Manual, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Preference kinds. Only the restrictive ones affect eligibility; the rest are advisory and surfaced as
/// diagnostics, so a preference never silently grants anything.</summary>
public static class PreferenceType
{
    public const string PreferredChannel = "preferred-channel";
    public const string DoNotContact = "do-not-contact";
    public const string DoNotVisit = "do-not-visit";
    public const string PreferredVisitWindow = "preferred-visit-window";
    public const string LanguagePreference = "language-preference";
    public const string ContentPreference = "content-preference";
    public const string FrequencyCap = "frequency-cap";
    public const string TopicInterest = "topic-interest";

    public static readonly IReadOnlyList<string> All = new[]
    {
        PreferredChannel, DoNotContact, DoNotVisit, PreferredVisitWindow, LanguagePreference, ContentPreference,
        FrequencyCap, TopicInterest
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>The boolean restriction types: their value must be a boolean literal, and only <c>true</c> blocks.</summary>
    public static bool IsBooleanRestriction(string? value) => Normalize(value) switch
    {
        DoNotContact or DoNotVisit => true,
        _ => false
    };
}
