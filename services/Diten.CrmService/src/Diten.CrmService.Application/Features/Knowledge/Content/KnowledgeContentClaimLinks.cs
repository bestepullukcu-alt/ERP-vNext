using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.Content;

/// <summary>A coded content ↔ claim failure; <see cref="To{T}"/> renders the <c>[code, message]</c> error pair.</summary>
internal sealed record ContentClaimFailure(string Code, string Message, int StatusCode)
{
    public Response<T> To<T>() => Response<T>.Fail(new[] { Code, Message }, StatusCode);
}

/// <summary>
/// WP-CL-BE-6 (claims v2, D4) — the rules of <see cref="KnowledgeContent.ClaimRefs"/>. Claim data is only READ here
/// (repository interfaces); nothing on the claim side is written.
/// <list type="bullet">
/// <item><b>Save</b> (<see cref="ResolveAsync"/>): the claim record exists in the tenant and carries the ref's
/// ClaimCode; a country version belongs to the same ClaimCode and CountryCode; content ProductId ≠ claim ProductId (both
/// set) → <c>claim_product_mismatch</c>. Status is free — a draft content may bind a draft claim.</item>
/// <item><b>Publish gate</b> (<see cref="CheckPublishAsync"/>): every ref must be usable — the country version (or, for
/// a core ref, the claim record) <c>approved</c> or <c>review-required</c>, else 409 <c>claim_not_approved</c>; a
/// country-version ref needs the content language among the version's text languages, else 409
/// <c>claim_language_mismatch</c>. <c>review-required</c> does not block (the reads flag it).</item>
/// </list>
/// An empty ref list is never checked — content without claims behaves exactly as before.
/// </summary>
internal static class KnowledgeContentClaimLinks
{
    public static bool IsUsable(string? status)
        => string.Equals(status, ClaimStatuses.Approved, StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, ClaimStatuses.ReviewRequired, StringComparison.OrdinalIgnoreCase);

    public static bool NeedsReview(string? status)
        => string.Equals(status, ClaimStatuses.ReviewRequired, StringComparison.OrdinalIgnoreCase);

    /// <summary>Validates <paramref name="input"/> and returns the normalised refs to store (ClaimCode taken from the
    /// claim record, CountryCode from the version).</summary>
    public static async Task<(List<KnowledgeContentClaimRef>? Refs, ContentClaimFailure? Failure)> ResolveAsync(
        IReadOnlyList<KnowledgeContentClaimRefInput> input,
        Guid tenantId,
        Guid? contentProductId,
        IClaimRepository? claims,
        IClaimCountryVersionRepository? versions,
        CancellationToken cancellationToken)
    {
        var refs = new List<KnowledgeContentClaimRef>();
        if (input.Count == 0)
        {
            return (refs, null);
        }

        if (input.Count > KnowledgeContentClaimRef.MaxPerContent)
        {
            return (null, new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimRefsTooMany,
                $"A content may reference at most {KnowledgeContentClaimRef.MaxPerContent} claims.", 400));
        }

        if (claims is null || versions is null)
        {
            return (null, new ContentClaimFailure(KnowledgeContentClaimErrors.DependencyUnavailable,
                "Claim references cannot be verified (claim store unavailable).", 503));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < input.Count; i++)
        {
            var line = input[i];
            var code = line.ClaimCode?.Trim();
            var country = string.IsNullOrWhiteSpace(line.CountryCode) ? null : line.CountryCode.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code) || line.ClaimId == Guid.Empty
                || line.CountryVersionId == Guid.Empty
                || (country is not null && line.CountryVersionId is null))
            {
                return (null, new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimRefInvalid,
                    $"ClaimRefs[{i}]: ClaimCode and ClaimId are required; CountryCode needs a CountryVersionId.", 400));
            }

            var claim = await claims.GetByIdAsync(tenantId, line.ClaimId, cancellationToken);
            if (claim is null)
            {
                return (null, new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimNotFound,
                    $"ClaimRefs[{i}]: ClaimId does not reference a claim in this tenant.", 400));
            }

            if (!string.Equals(claim.ClaimCode, code, StringComparison.OrdinalIgnoreCase))
            {
                return (null, new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimRefMismatch,
                    $"ClaimRefs[{i}]: ClaimId does not belong to ClaimCode '{code}'.", 400));
            }

            if (line.CountryVersionId is { } versionId)
            {
                var version = await versions.GetByIdAsync(tenantId, versionId, cancellationToken);
                if (version is null)
                {
                    return (null, new ContentClaimFailure(KnowledgeContentClaimErrors.CountryVersionNotFound,
                        $"ClaimRefs[{i}]: CountryVersionId does not reference a country version in this tenant.", 400));
                }

                if (!string.Equals(version.ClaimCode, claim.ClaimCode, StringComparison.OrdinalIgnoreCase)
                    || (country is not null
                        && !string.Equals(version.CountryCode, country, StringComparison.OrdinalIgnoreCase)))
                {
                    return (null, new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimRefMismatch,
                        $"ClaimRefs[{i}]: the country version does not belong to this ClaimCode / CountryCode.", 400));
                }

                country = version.CountryCode.Trim().ToUpperInvariant();
            }

            if (contentProductId is { } contentProduct && contentProduct != Guid.Empty
                && claim.ProductId is { } claimProduct && claimProduct != Guid.Empty
                && contentProduct != claimProduct)
            {
                return (null, new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimProductMismatch,
                    $"ClaimRefs[{i}]: the claim is about another product than this content.", 400));
            }

            if (!seen.Add(claim.ClaimCode + "|" + (country ?? string.Empty)))
            {
                return (null, new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimRefDuplicate,
                    $"ClaimRefs[{i}]: ClaimCode '{claim.ClaimCode}' is already referenced for this country.", 400));
            }

            refs.Add(new KnowledgeContentClaimRef
            {
                ClaimCode = claim.ClaimCode,
                ClaimId = claim.Id,
                CountryVersionId = line.CountryVersionId,
                CountryCode = country
            });
        }

        return (refs, null);
    }

    /// <summary>The publish gate. <paramref name="refs"/> empty ⇒ no gate (unchanged behaviour).</summary>
    public static async Task<ContentClaimFailure?> CheckPublishAsync(
        IReadOnlyList<KnowledgeContentClaimRef> refs,
        string languageCode,
        Guid tenantId,
        IClaimRepository? claims,
        IClaimCountryVersionRepository? versions,
        CancellationToken cancellationToken)
    {
        if (refs.Count == 0)
        {
            return null;
        }

        if (claims is null || versions is null)
        {
            return new ContentClaimFailure(KnowledgeContentClaimErrors.DependencyUnavailable,
                "Claim references cannot be verified (claim store unavailable).", 503);
        }

        foreach (var r in refs)
        {
            if (r.CountryVersionId is { } versionId)
            {
                var version = await versions.GetByIdAsync(tenantId, versionId, cancellationToken);
                if (version is null || version.IsArchived() || !IsUsable(version.Status))
                {
                    return new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimNotApproved,
                        $"Claim '{r.ClaimCode}' ({r.CountryCode}) is not approved in that country; the content cannot be published.",
                        409);
                }

                var language = languageCode.Trim();
                if (!version.Texts.Any(t => string.Equals(t.LanguageCode?.Trim(), language, StringComparison.OrdinalIgnoreCase)))
                {
                    return new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimLanguageMismatch,
                        $"Claim '{r.ClaimCode}' ({r.CountryCode}) has no text in content language '{language}'.", 409);
                }

                continue;
            }

            var claim = await claims.GetByIdAsync(tenantId, r.ClaimId, cancellationToken);
            if (claim is null || claim.IsArchived() || !IsUsable(claim.Status))
            {
                return new ContentClaimFailure(KnowledgeContentClaimErrors.ClaimNotApproved,
                    $"Claim '{r.ClaimCode}' is not approved; the content cannot be published.", 409);
            }
        }

        return null;
    }

    public static bool SameRefs(IReadOnlyList<KnowledgeContentClaimRef> a, IReadOnlyList<KnowledgeContentClaimRef> b)
        => a.Count == b.Count && a.Zip(b).All(p =>
            p.First.ClaimId == p.Second.ClaimId
            && p.First.CountryVersionId == p.Second.CountryVersionId
            && string.Equals(p.First.ClaimCode, p.Second.ClaimCode, StringComparison.Ordinal)
            && string.Equals(p.First.CountryCode, p.Second.CountryCode, StringComparison.Ordinal));

    /// <summary>Audit detail — ids and counts only, never claim wording.</summary>
    public static string AuditDetail(
        string contentCode, IReadOnlyList<KnowledgeContentClaimRef> before, IReadOnlyList<KnowledgeContentClaimRef> after)
    {
        static string Key(KnowledgeContentClaimRef r) => $"{r.ClaimId}|{r.CountryVersionId}";
        var beforeKeys = before.Select(Key).ToHashSet();
        var afterKeys = after.Select(Key).ToHashSet();
        return $"{contentCode};claimRefs={after.Count};added={afterKeys.Count(k => !beforeKeys.Contains(k))};"
               + $"removed={beforeKeys.Count(k => !afterKeys.Contains(k))}";
    }

    /// <summary>Detail-read enrichment: each ref with the CURRENT status of its claim record / country version.</summary>
    public static async Task<IReadOnlyList<KnowledgeContentClaimRefDto>> EnrichAsync(
        IReadOnlyList<KnowledgeContentClaimRef> refs,
        Guid tenantId,
        IClaimRepository? claims,
        IClaimCountryVersionRepository? versions,
        CancellationToken cancellationToken)
    {
        var result = new List<KnowledgeContentClaimRefDto>(refs.Count);
        foreach (var r in refs)
        {
            var claim = claims is null ? null : await claims.GetByIdAsync(tenantId, r.ClaimId, cancellationToken);
            var version = r.CountryVersionId is { } vid && versions is not null
                ? await versions.GetByIdAsync(tenantId, vid, cancellationToken)
                : null;
            var needsReview = r.CountryVersionId is null ? NeedsReview(claim?.Status) : NeedsReview(version?.Status);
            result.Add(new KnowledgeContentClaimRefDto(
                r.ClaimCode, r.ClaimId, r.CountryVersionId, r.CountryCode, claim?.Status, version?.Status, needsReview));
        }

        return result;
    }
}
