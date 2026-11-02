namespace Diten.Platform.Domain.Enums.RecordCodes;

/// <summary>
/// MOD-0040-FU02 (RCS-001 §5.3) — where a record code's prefix comes from. On the wire the NAME is used
/// (<c>LegalEntity</c> | <c>Group</c> | <c>Platform</c>); a missing value is <see cref="LegalEntity"/>.
/// </summary>
public enum RecordCodePrefixScope
{
    /// <summary>The company short code (MDM <c>LegalEntity.Code</c>).</summary>
    LegalEntity = 1,

    /// <summary>The tenant's group prefix (the document <c>OrgPrefix</c>, aligned and locked).</summary>
    Group = 2,

    /// <summary>No prefix (platform-global). Defined in the contract; opened in DL-4 (K-11).</summary>
    Platform = 3
}
