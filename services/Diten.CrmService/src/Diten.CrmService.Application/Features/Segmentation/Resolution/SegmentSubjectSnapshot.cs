namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 — the NATIVE (class N) projection of one candidate subject, produced by the single Phase-1 Mongo
/// query. It is a read projection and nothing more: no Account or Contact aggregate is loaded, mutated or copied, and
/// the heavy PII/size members (<c>Contact.PhotoDataUri</c>, <c>Account.LogoDataUri</c>) are deliberately absent.
/// One shape serves both subject types; the fields a subject type does not have are simply null, and the catalog
/// already forbids asking for them (an <c>account</c> segment cannot use a <c>contact.*</c> attribute).
/// <para><see cref="DisplayName"/> rides on this SAME projection so a resolution can report a readable name instead of
/// a raw id. It costs no extra read and no extra round-trip by construction — if it ever needed one, the N+1 ban would
/// forbid it. It is a display label only: no rule is ever evaluated against it.</para>
/// </summary>
public sealed record SegmentSubjectSnapshot(
    Guid SubjectId,
    string SubjectType,
    string? DisplayName,
    string? Type,
    string? Category,
    string? Status,
    string? Country,
    string? City,
    string? District,
    Guid? ParentAccountId,
    DateTimeOffset CreatedAt,
    string? Specialty,
    string? ProfessionalTitle,
    string? Department,
    string? Gender,
    string? PreferredLanguage)
{
    /// <summary>
    /// MOD-0167 FU02 (WP-SEG-F) — the linked account name (the contact's workplace), carried additively for the sample
    /// label only. It is filled from the primary <see cref="SegmentLinkProjection"/> during resolution (from the SAME
    /// bulk link+account read, no extra query), is null for an account subject and for any contact with no named link,
    /// and — like <see cref="DisplayName"/> and <see cref="SecondaryLabel"/> — is a display value only: no rule is ever
    /// evaluated against it.
    /// </summary>
    public string? Workplace { get; init; }

    /// <summary>
    /// MOD-0167 FU02 (WP-SEG-D + WP-SEG-F) — the SECONDARY display label a preview sample or a resolved member shows
    /// under the name, in place of a raw id. It is derived purely from fields THIS projection already carries, so it
    /// costs no extra read and no extra round-trip: a contact prefers <see cref="Specialty"/> then
    /// <see cref="ProfessionalTitle"/>, an account prefers <see cref="Type"/> then <see cref="Category"/>. The secondary
    /// segment is the contact's <see cref="Workplace"/> when known (WP-SEG-F), falling back to <see cref="City"/>; an
    /// account always uses its <see cref="City"/>. When both are present the label reads <c>"core · secondary"</c>, and a
    /// bare secondary is used on its own when no role/type is known. Like <see cref="DisplayName"/> it is a display label
    /// only — no rule is ever evaluated against it. Null when nothing readable can be formed.
    /// </summary>
    public string? SecondaryLabel
    {
        get
        {
            var isContact = string.Equals(SubjectType, "contact", StringComparison.OrdinalIgnoreCase);
            var core = isContact
                ? FirstNonBlank(Specialty, ProfessionalTitle)
                : FirstNonBlank(Type, Category);
            var secondary = isContact
                ? FirstNonBlank(Workplace, City)
                : (string.IsNullOrWhiteSpace(City) ? null : City.Trim());

            if (core is not null && secondary is not null)
            {
                return $"{core} · {secondary}";
            }

            return core ?? secondary;
        }
    }

    private static string? FirstNonBlank(string? primary, string? secondary)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        return string.IsNullOrWhiteSpace(secondary) ? null : secondary.Trim();
    }
}
