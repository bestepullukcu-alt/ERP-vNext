namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W3 (BE-a, K-W2) — the tenant's visit evidence rules: ONE document per tenant. When it does not exist the
/// <see cref="VisitEvidencePolicyDefaults"/> apply, and a read never writes it (only the manage PUT does).
/// </summary>
public sealed class VisitEvidencePolicy : EntityBase
{
    /// <summary><see cref="VisitLocationModes"/> — <c>required</c> (refuse a start without a good fix) or <c>info</c>
    /// (record + flag).</summary>
    public string LocationMode { get; set; } = VisitEvidencePolicyDefaults.LocationMode;

    public int RadiusMeters { get; set; } = VisitEvidencePolicyDefaults.RadiusMeters;
    public int MaxAccuracyMeters { get; set; } = VisitEvidencePolicyDefaults.MaxAccuracyMeters;

    /// <summary><see cref="VisitPhotoModes"/>.</summary>
    public string PhotoMode { get; set; } = VisitEvidencePolicyDefaults.PhotoMode;

    public bool DoctorConfirmation { get; set; } = VisitEvidencePolicyDefaults.DoctorConfirmation;
    public bool DurationConsistency { get; set; } = VisitEvidencePolicyDefaults.DurationConsistency;
    public int ShortVisitSeconds { get; set; } = VisitEvidencePolicyDefaults.ShortVisitSeconds;

    /// <summary><see cref="AccountWithoutLocationModes"/> — an institution with no coordinates.</summary>
    public string AccountWithoutLocation { get; set; } = VisitEvidencePolicyDefaults.AccountWithoutLocation;

    public string? UpdatedBy { get; set; }

    /// <summary>WP-VW-W3E (W3E-BE-a, K-W3E-1) — <see cref="VisitQrModes"/>: the institution QR code at a visit start
    /// (<c>off</c> | <c>optional</c> | <c>required</c>). An older document without it reads the default (<c>off</c>).</summary>
    public string QrMode { get; set; } = VisitEvidencePolicyDefaults.QrMode;

    /// <summary>WP-VW-W3E (BE-b, §2.1) — <see cref="VisitAntiFraudModes"/>: <c>off</c> (the device declaration is recorded
    /// only) or <c>flag</c> (recorded + flagged). Never refuses a start (K-W3E-3). An older document reads the default.</summary>
    public string AntiFraudMode { get; set; } = VisitEvidencePolicyDefaults.AntiFraudMode;
}

/// <summary>
/// WP-VW-W3L (W3-BE-d, K-L1 / K-L2) — a LAYER of the visit evidence rules above the tenant: a legal entity (the rep's
/// Platform primary seat) or a business unit (the rep's primary territory assignment). It holds ONLY the fields it changes
/// (null = inherited from the layer below); the tenant layer is the tenant's <see cref="VisitEvidencePolicy"/> document,
/// which stays as it is (an older document reads as the tenant layer — nothing is backfilled, the MOD-0165-FU07 pattern).
/// Keyed (TenantId, ScopeType, ScopeRef), versioned; "remove the scope" archives it (K-L6), never deletes it.
/// </summary>
public sealed class VisitEvidencePolicyLayer : EntityBase
{
    /// <summary><see cref="VisitEvidencePolicyScopeTypes.LegalEntity"/> | <see cref="VisitEvidencePolicyScopeTypes.BusinessUnit"/>.</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>The legal entity id ("D" format) or the MOD-0048 <c>business-unit</c> value code.</summary>
    public string ScopeRef { get; set; } = string.Empty;

    /// <summary><see cref="VisitEvidencePolicyLayerStatuses"/>.</summary>
    public string Status { get; set; } = VisitEvidencePolicyLayerStatuses.Active;

    public string? LocationMode { get; set; }
    public int? RadiusMeters { get; set; }
    public int? MaxAccuracyMeters { get; set; }
    public string? PhotoMode { get; set; }
    public bool? DoctorConfirmation { get; set; }
    public bool? DurationConsistency { get; set; }
    public int? ShortVisitSeconds { get; set; }
    public string? AccountWithoutLocation { get; set; }
    public string? QrMode { get; set; }
    public string? AntiFraudMode { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsActive()
        => !IsDeleted && string.Equals(Status, VisitEvidencePolicyLayerStatuses.Active, StringComparison.Ordinal);
}

/// <summary>WP-VW-W3L — the scope types of the evidence rules (the history's <c>Scope</c> uses the same words).</summary>
public static class VisitEvidencePolicyScopeTypes
{
    public const string Tenant = "tenant";
    public const string LegalEntity = "legal-entity";
    public const string BusinessUnit = "business-unit";

    /// <summary>The layers a scope write may name (the tenant layer is the existing policy PUT).</summary>
    public static readonly IReadOnlyList<string> Layers = new[] { LegalEntity, BusinessUnit };
}

public static class VisitEvidencePolicyLayerStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";
}

public static class VisitLocationModes
{
    public const string Required = "required";
    public const string Info = "info";

    public static readonly IReadOnlyList<string> All = new[] { Required, Info };
}

public static class VisitPhotoModes
{
    public const string Required = "required";
    public const string Optional = "optional";

    public static readonly IReadOnlyList<string> All = new[] { Required, Optional };
}

public static class AccountWithoutLocationModes
{
    public const string AllowFlagged = "allow_flagged";
    public const string Block = "block";

    public static readonly IReadOnlyList<string> All = new[] { AllowFlagged, Block };
}

/// <summary>WP-VW-W3E (BE-b) — the anti-fraud mode. <c>block</c> comes with W3E-ATTEST (server-verified tokens); today it
/// is an invalid value.</summary>
public static class VisitAntiFraudModes
{
    public const string Off = "off";
    public const string Flag = "flag";

    public static readonly IReadOnlyList<string> All = new[] { Off, Flag };
}

/// <summary>K-W2 defaults (location required, photo optional) and the CT defaults / bounds of the package (§2.2).</summary>
public static class VisitEvidencePolicyDefaults
{
    public const string LocationMode = VisitLocationModes.Required;
    public const int RadiusMeters = 300;
    public const int MaxAccuracyMeters = 150;
    public const string PhotoMode = VisitPhotoModes.Optional;
    public const bool DoctorConfirmation = true;
    public const bool DurationConsistency = true;
    public const int ShortVisitSeconds = 120;
    public const string AccountWithoutLocation = AccountWithoutLocationModes.AllowFlagged;

    public const int MinRadiusMeters = 50;
    public const int MaxRadiusMeters = 5000;
    public const int MinAccuracyMeters = 10;
    public const int MaxAccuracyMetersBound = 1000;
    public const int MinShortVisitSeconds = 30;
    public const int MaxShortVisitSeconds = 1800;

    /// <summary>WP-VW-W3E (W3E-BE-a) — the QR code is off until a tenant turns it on.</summary>
    public const string QrMode = VisitQrModes.Off;

    // WP-VW-W3E (BE-b, §2.1)
    public const string AntiFraudMode = VisitAntiFraudModes.Off;
}
