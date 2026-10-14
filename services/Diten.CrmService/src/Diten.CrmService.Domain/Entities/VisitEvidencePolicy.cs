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
}
