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
    string? PreferredLanguage);
