namespace Diten.Platform.Domain.Entities.ESignature;

// MOD-0022 — enums of the e-signature service. Wire names (ESIGN-BUNDLE §2) are the camelCase member names; they also
// enter the signature digest, so a member is never renamed (ESignatureWire maps them).

/// <summary>How the password was proven for the consumed ticket (AuthService ESignReauthKind).</summary>
public enum ESignatureReauthKind
{
    Password = 0,
    Window = 1
}

/// <summary>The visual signature of a record (KS-8 / KS-10: appearance only, the legal bond is password + meaning).</summary>
public enum ESignatureAppearanceType
{
    None = 0,
    Drawn = 1,
    Sample = 2
}

/// <summary>Visual-signature rule of a policy row (KS-8).</summary>
public enum ESignatureVisualMode
{
    None = 0,
    Optional = 1,
    SavedAccepted = 2,
    DrawRequired = 3
}

/// <summary>Second factor (KS-11): v1 has none; the policy column is shown disabled ("Yakında").</summary>
public enum ESignatureSecondFactor
{
    None = 0,
    NotAvailable = 1
}

public enum ESignaturePolicyScope
{
    Tenant = 0,
    LegalEntity = 1
}

public enum ESignatureRequestStatus
{
    Waiting = 0,
    Signed = 1,
    Rejected = 2,
    Expired = 3,
    Cancelled = 4
}

public enum ESignatureVerificationStatus
{
    Valid = 0,
    Changed = 1,
    Invalidated = 2,
    Tampered = 3,
    SubjectUnresolvable = 4
}

public enum ESignatureExportFormat
{
    Pdf = 0,
    Csv = 1
}

/// <summary>camelCase wire names (ESIGN-BUNDLE §2) — the single place enum values become strings.</summary>
public static class ESignatureWire
{
    public static string Name<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value)
               && !int.TryParse(value, out _)
               && Enum.TryParse(value.Trim(), ignoreCase: true, out result)
               && Enum.IsDefined(result);
    }
}
