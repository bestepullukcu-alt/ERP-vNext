using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge;

/// <summary>
/// MOD-0162 FU02 structural validation. Every rule returns an error string (400 message) or null. The content/taxonomy
/// vocabulary is validated in-domain against the <c>Knowledge*</c> / <c>Taxonomy*</c> constants — it is structural, so
/// it never fails open on an unpublished MOD-0048 set. Reference fields (Subject/Topic/AudienceProfile/Concept/Brand/
/// Product/Campaign/Segment) are validated at <b>format level only</b>: an explicitly supplied empty GUID is a caller
/// error, everything else is stored as provenance (Subject archived-existence is checked in the handler, which has repo
/// access).
/// </summary>
public static class KnowledgeValidation
{
    // ---------------- KnowledgeContent ----------------

    public static string? ValidateContentCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? "ContentCode is required." : null;

    public static string? ValidateContentTitle(string? title)
        => string.IsNullOrWhiteSpace(title) ? "ContentTitle is required." : null;

    public static string? ValidateContentType(string? type)
        => KnowledgeContentTypes.IsValid(type)
            ? null
            : $"ContentType is required and must be one of: {string.Join(", ", KnowledgeContentTypes.All)}.";

    public static string? ValidateContentStatus(string? status)
        => string.IsNullOrWhiteSpace(status) || KnowledgeContentStatuses.IsValid(status)
            ? null
            : $"ContentStatus must be one of: {string.Join(", ", KnowledgeContentStatuses.All)}. " +
              "Content is never hard-deleted; closing it is the archive endpoint.";

    public static string? ValidateSource(string? source)
        => string.IsNullOrWhiteSpace(source) || KnowledgeContentSources.IsValid(source)
            ? null
            : $"Source must be one of: {string.Join(", ", KnowledgeContentSources.All)}.";

    public static string? ValidateLanguageCode(string? language)
        => string.IsNullOrWhiteSpace(language) ? "LanguageCode is required." : null;

    public static string? ValidateContentVersion(string? version)
        => string.IsNullOrWhiteSpace(version) ? "ContentVersion is required." : null;

    public static string? ValidateRequiredSubject(Guid subjectId)
        => subjectId == Guid.Empty ? "SubjectId is required and cannot be empty." : null;

    public static string? ValidateEffectiveFrom(DateTimeOffset effectiveFrom)
        => effectiveFrom == default ? "EffectiveFrom is required." : null;

    public static string? ValidateEffectiveRange(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
        => effectiveTo is { } to && to < effectiveFrom
            ? "EffectiveTo cannot be earlier than EffectiveFrom."
            : null;

    /// <summary>At least one content pointer must be present — content that points nowhere is not authorable, and a
    /// binary is never stored here (FileRef is a MOD-0028/0029 document reference).</summary>
    public static string? ValidateContentPointers(string? bodyRef, string? assetRef, string? fileRef, string? url)
        => string.IsNullOrWhiteSpace(bodyRef)
           && string.IsNullOrWhiteSpace(assetRef)
           && string.IsNullOrWhiteSpace(fileRef)
           && string.IsNullOrWhiteSpace(url)
            ? "At least one of ContentBodyRef / ContentAssetRef / FileRef / Url is required."
            : null;

    // ---------------- Taxonomy (Subject / Topic / AudienceProfile) ----------------

    public static string? ValidateCode(string? code, string fieldName)
        => string.IsNullOrWhiteSpace(code) ? $"{fieldName} is required." : null;

    public static string? ValidateName(string? name, string fieldName)
        => string.IsNullOrWhiteSpace(name) ? $"{fieldName} is required." : null;

    public static string? ValidateTaxonomyStatus(string? status)
        => string.IsNullOrWhiteSpace(status) || TaxonomyStatuses.IsValid(status)
            ? null
            : $"Status must be one of: {string.Join(", ", TaxonomyStatuses.All)}. " +
              "A taxonomy row is never hard-deleted; closing it is the archive endpoint.";

    public static string? ValidateProfileType(string? profileType)
        => string.IsNullOrWhiteSpace(profileType) || AudienceProfileTypes.IsValid(profileType)
            ? null
            : $"ProfileType must be one of: {string.Join(", ", AudienceProfileTypes.All)}.";

    // ---------------- Shared ----------------

    public static string? ValidateOptionalReference(Guid? value, string fieldName)
        => value is { } id && id == Guid.Empty
            ? $"{fieldName} must be a non-empty identifier when supplied (omit the field instead)."
            : null;

    /// <summary>External references: SourceSystem + ExternalId mandatory per line, at most one primary, and a duplicate
    /// (SourceSystem, ExternalId) inside the same payload is a conflict. Returns (error, isConflict); the caller maps a
    /// conflict to 409 and everything else to 400.</summary>
    public static (string? Error, bool IsConflict) ValidateExternalReferences(
        IReadOnlyList<KnowledgeExternalReferenceInput>? references)
    {
        if (references is null || references.Count == 0)
        {
            return (null, false);
        }

        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference.SourceSystem))
            {
                return ("ExternalReferences[].SourceSystem is required.", false);
            }

            if (string.IsNullOrWhiteSpace(reference.ExternalId))
            {
                return ("ExternalReferences[].ExternalId is required.", false);
            }
        }

        if (references.Count(r => r.IsPrimary) > 1)
        {
            return ("At most one external reference may be marked IsPrimary.", false);
        }

        var duplicate = references
            .GroupBy(r => (r.SourceSystem.Trim().ToLowerInvariant(), r.ExternalId.Trim()))
            .FirstOrDefault(g => g.Count() > 1);

        return duplicate is null
            ? (null, false)
            : ($"Duplicate external mapping '{duplicate.Key.Item1}/{duplicate.Key.Item2}' in the payload.", true);
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
