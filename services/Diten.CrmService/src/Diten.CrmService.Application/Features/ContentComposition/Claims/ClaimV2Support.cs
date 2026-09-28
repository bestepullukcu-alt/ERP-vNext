using System.Globalization;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>WP-CL-BE-1 (claims v2) — stable error codes. Every failure carries <c>Errors = [code, message]</c> (the
/// StrategyTemplate convention), so the FE can branch on the code rather than on the wording.</summary>
public static class ClaimErrorCodes
{
    public const string CoreNotApproved = "core_not_approved";
    public const string CountryClosed = "country_closed";
    public const string CountryVersionExists = "country_version_exists";
    public const string CountryHasVersion = "country_has_version";
    public const string CountryAlreadyClosed = "country_already_closed";
    public const string CountryNotClosed = "country_not_closed";
    public const string NotApplicable = "not_applicable";
    public const string ReferenceSetMissing = "reference_set_missing";
    public const string InvalidReferenceValue = "invalid_reference_value";
    public const string LanguageNotAllowed = "language_not_allowed";
    public const string DuplicateLanguage = "duplicate_language";
    public const string TextsRequired = "texts_required";
    public const string LanguagesIncomplete = "languages_incomplete";
    public const string AdaptationReasonRequired = "adaptation_reason_required";
    public const string AudienceNotNarrowing = "audience_not_narrowing";
    public const string AudienceNotFound = "audience_not_found";
    public const string ProductRequired = "product_required";
    public const string ProductNotFound = "product_not_found";
    public const string DependencyUnavailable = "dependency_unavailable";
    public const string InvalidKind = "invalid_kind";
    public const string LocalCountryRequired = "local_country_required";
    public const string VersionLocked = "version_locked";
    public const string OpenVersionExists = "open_version_exists";
    public const string InvalidState = "invalid_state";
    public const string InvalidValidity = "invalid_validity";
    public const string Required = "required";

    // WP-CL-BE-4 — approval via MOD-0023 workflow.
    public const string InvalidStatus = "invalid_status";
    public const string InReviewLocked = "in_review_locked";
    public const string ApprovalViaWorkflowOnly = "approval_via_workflow_only";
    public const string ApprovalTemplateMissing = "approval_template_missing";
    public const string ApprovalForbidden = "approval_forbidden";
    public const string WorkflowUnavailable = "workflow_unavailable";
    public const string WorkflowRequestRejected = "workflow_request_rejected";
    public const string NoOpenReview = "no_open_review";
    public const string WithdrawNotPossible = "withdraw_not_possible";
}

/// <summary>WP-CL-BE-1 — the MOD-0048 reference sets the claims v2 rules read. Only the set CODES live here; the values
/// (countries, languages, reasons, adaptation types) are always read from BRD, never compiled in.</summary>
public static class ClaimReferenceSets
{
    /// <summary>Global — the country axis (matrix columns, closure / version country).</summary>
    public const string CountryCodes = "COUNTRY_CODES";

    /// <summary>Global — per country the <see cref="LanguagesAttribute"/> list (e.g. UZ = "uz,ru").</summary>
    public const string CountryContentLanguages = "country-content-languages";

    public const string LanguagesAttribute = "Languages";

    /// <summary>Tenant — single-choice reason a claim × country cell is closed.</summary>
    public const string ClosureReason = "claim-country-closure-reason";

    /// <summary>Tenant — verbatim / narrowed / softened.</summary>
    public const string AdaptationType = "claim-adaptation-type";

    /// <summary>The one adaptation code that needs no reason (a rule, not a list).</summary>
    public const string VerbatimAdaptation = "verbatim";

    /// <summary>Default language of a core claim's wording.</summary>
    public const string DefaultCoreLanguage = "en";
}

/// <summary>WP-CL-BE-1 — coverage matrix cell states.</summary>
public static class ClaimCoverageStates
{
    public const string Approved = ClaimStatuses.Approved;
    public const string InReview = ClaimStatuses.InReview;
    public const string Draft = ClaimStatuses.Draft;
    public const string ReviewRequired = ClaimStatuses.ReviewRequired;
    public const string Closed = "closed";
    public const string NotOpened = "not-opened";
    public const string NotApplicable = "not-applicable";
}

/// <summary>WP-CL-BE-1 — <c>Crm:Claims:ExpiringWindowDays</c> (a country version is "expiring" when its ValidTo is
/// within this many days of today). Application consumes the interface; Infrastructure reads configuration.</summary>
public interface IClaimCoverageSettings
{
    int ExpiringWindowDays { get; }
}

public static class ClaimCoverageDefaults
{
    public const int ExpiringWindowDays = 60;
}

/// <summary>A coded failure; <see cref="To{T}"/> renders the <c>[code, message]</c> error pair.</summary>
internal sealed record ClaimFailure(string Code, string Message, int StatusCode = 400)
{
    public Response<T> To<T>() => Response<T>.Fail(new[] { Code, Message }, StatusCode);
}

/// <summary>WP-CL-BE-1 — fail-closed reference / product / audience checks shared by the claims v2 handlers. A missing
/// seam or an unpublished set is a controlled 400 (<c>reference_set_missing</c>) — never a silent default.</summary>
internal static class ClaimV2Checks
{
    public static string NormalizeCountry(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static string NormalizeLanguage(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    public static async Task<ClaimFailure?> ValidateReferenceAsync(
        IReferenceDataValidator? validator, string setCode, string value, string field, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ClaimFailure(ClaimErrorCodes.Required, $"{field} is required.");
        }

        if (validator is null)
        {
            return new ClaimFailure(ClaimErrorCodes.ReferenceSetMissing,
                $"Reference set '{setCode}' cannot be read; {field} cannot be validated.");
        }

        var result = await validator.ValidateAsync(setCode, value, ct);
        return result.Status switch
        {
            ReferenceValidationStatus.Valid => null,
            ReferenceValidationStatus.SetMissing => new ClaimFailure(ClaimErrorCodes.ReferenceSetMissing,
                $"Reference set '{setCode}' is not published; {field} cannot be validated."),
            _ => new ClaimFailure(ClaimErrorCodes.InvalidReferenceValue,
                $"{field} '{value}' is not a valid value of reference set '{setCode}'.")
        };
    }

    /// <summary>The country's content languages from <c>country-content-languages[country].Languages</c>. A missing
    /// set, value or attribute is <c>reference_set_missing</c>.</summary>
    public static async Task<(IReadOnlyList<string> Languages, ClaimFailure? Failure)> GetCountryLanguagesAsync(
        IReferenceMetadataReader? metadata, string countryCode, CancellationToken ct)
    {
        var missing = new ClaimFailure(ClaimErrorCodes.ReferenceSetMissing,
            $"Content languages for country '{countryCode}' are not available "
            + $"(reference set '{ClaimReferenceSets.CountryContentLanguages}', attribute "
            + $"'{ClaimReferenceSets.LanguagesAttribute}').");

        if (metadata is null)
        {
            return (Array.Empty<string>(), missing);
        }

        var attributes = await metadata.GetValueAttributesAsync(
            ClaimReferenceSets.CountryContentLanguages, countryCode, ct);
        if (attributes is null
            || !attributes.TryGetValue(ClaimReferenceSets.LanguagesAttribute, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return (Array.Empty<string>(), missing);
        }

        var languages = raw.Split(new[] { ',', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeLanguage).Where(l => l.Length > 0).Distinct().ToList();
        return languages.Count == 0 ? (Array.Empty<string>(), missing) : (languages, null);
    }

    public static async Task<ClaimFailure?> ValidateProductAsync(
        IStrategyTemplateProductReferenceValidator? validator, Guid productId, CancellationToken ct)
    {
        if (validator is null)
        {
            return new ClaimFailure(ClaimErrorCodes.DependencyUnavailable,
                "The product master cannot be reached to prove the product. Nothing was saved.", 503);
        }

        var outcome = await validator.ValidateAsync(
            IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct, productId, ct);
        return outcome switch
        {
            IStrategyTemplateProductReferenceValidator.Outcome.Valid => null,
            IStrategyTemplateProductReferenceValidator.Outcome.NotFound => new ClaimFailure(
                ClaimErrorCodes.ProductNotFound, $"Global product '{productId}' does not exist."),
            _ => new ClaimFailure(ClaimErrorCodes.DependencyUnavailable,
                "The product master could not be reached to prove the product. Nothing was saved.", 503)
        };
    }

    public static async Task<ClaimFailure?> ValidateAudiencesAsync(
        IAudienceProfileRepository? audiences, Guid tenantId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return null;
        }

        if (audiences is null)
        {
            return new ClaimFailure(ClaimErrorCodes.DependencyUnavailable,
                "Audience profiles cannot be read. Nothing was saved.", 503);
        }

        foreach (var id in ids)
        {
            if (await audiences.GetByIdAsync(tenantId, id, ct) is null)
            {
                return new ClaimFailure(ClaimErrorCodes.AudienceNotFound, $"Audience profile '{id}' does not exist.");
            }
        }

        return null;
    }

    /// <summary>Cleans per-language inputs: trims, lower-cases the language, drops empty texts. Duplicate languages
    /// are a failure.</summary>
    public static (List<ClaimLocalizedText> Items, ClaimFailure? Failure) CleanTexts(
        IReadOnlyList<ClaimLocalizedTextInput>? input, string field)
    {
        var items = new List<ClaimLocalizedText>();
        foreach (var t in input ?? Array.Empty<ClaimLocalizedTextInput>())
        {
            var language = NormalizeLanguage(t?.LanguageCode);
            var text = t?.Text?.Trim() ?? string.Empty;
            if (language.Length == 0 || text.Length == 0)
            {
                continue;
            }

            if (items.Any(i => i.LanguageCode == language))
            {
                return (items, new ClaimFailure(ClaimErrorCodes.DuplicateLanguage,
                    $"{field} contains language '{language}' more than once."));
            }

            items.Add(new ClaimLocalizedText { LanguageCode = language, Text = text });
        }

        return (items, null);
    }
}

/// <summary>WP-CL-BE-1 — business version arithmetic (K12): core major line ("1.0" → "2.0"), country minor line
/// ("1.0" → "1.1").</summary>
internal static class ClaimVersioning
{
    public static bool TryParse(string? value, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Trim().TrimStart('v', 'V').Split('.');
        if (parts.Length is < 1 or > 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major))
        {
            return false;
        }

        return parts.Length == 1 || int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor);
    }

    public static int MajorOrZero(string? value) => TryParse(value, out var major, out _) ? major : 0;

    /// <summary>The next core major across every record of the code. When no sibling version parses (legacy free
    /// text), the WP rule applies: "2.0".</summary>
    public static string NextMajor(IEnumerable<string?> siblings)
    {
        var majors = siblings.Select(v => TryParse(v, out var m, out _) ? m : (int?)null).Where(m => m is not null)
            .Select(m => m!.Value).ToList();
        return majors.Count == 0 ? "2.0" : $"{majors.Max() + 1}.0";
    }

    /// <summary>The next country minor on the source's major line, above every sibling on that line.</summary>
    public static string NextMinor(string? source, IEnumerable<string?> siblings)
    {
        var major = TryParse(source, out var m, out _) ? m : 1;
        var maxMinor = siblings
            .Select(v => TryParse(v, out var sm, out var sn) && sm == major ? sn : -1)
            .DefaultIfEmpty(0).Max();
        return $"{major}.{Math.Max(maxMinor, 0) + 1}";
    }
}

/// <summary>WP-CL-BE-1 — shared selection rules over the records of one claim code and the country versions.</summary>
internal static class ClaimLines
{
    /// <summary>A live claim record: not archived and not superseded (inactive).</summary>
    public static bool IsLive(Claim c) => !c.IsArchived() && c.Status != ClaimStatuses.Inactive;

    /// <summary>The current record of a claim code: newest live major, else newest non-archived.</summary>
    public static Claim? Current(IEnumerable<Claim> records)
        => records.Where(c => !c.IsArchived())
            .OrderBy(c => IsLive(c) ? 0 : 1)
            .ThenByDescending(c => ClaimVersioning.MajorOrZero(c.ClaimVersion))
            .ThenByDescending(c => c.CreatedAt)
            .FirstOrDefault();

    public static Claim? LatestApproved(IEnumerable<Claim> records)
        => records.Where(c => c.IsApproved() && !c.IsArchived())
            .OrderByDescending(c => ClaimVersioning.MajorOrZero(c.ClaimVersion))
            .ThenByDescending(c => c.CreatedAt)
            .FirstOrDefault();

    public static bool IsExpiring(ClaimCountryVersion v, DateTimeOffset now, int windowDays)
        => v.ValidTo is { } to && to.UtcDateTime.Date <= now.UtcDateTime.Date.AddDays(windowDays);

    private static int Priority(string status) => status switch
    {
        ClaimStatuses.Approved => 0,
        ClaimStatuses.ReviewRequired => 1,
        ClaimStatuses.InReview => 2,
        _ => 3
    };

    /// <summary>The version a matrix cell shows: the effective one first (approved › review-required › in-review ›
    /// draft), newest minor on ties.</summary>
    public static ClaimCountryVersion? CellVersion(IEnumerable<ClaimCountryVersion> versions, string countryCode)
        => versions.Where(v => v.IsLive()
                && string.Equals(v.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => Priority(v.Status))
            .ThenByDescending(v => ClaimVersioning.TryParse(v.CountryVersion, out var ma, out var mi) ? ma * 10000 + mi : 0)
            .FirstOrDefault();

    public static ClaimCoverageCellDto Cell(
        Claim current, string countryCode, IEnumerable<ClaimCountryVersion> versionsOfCode,
        DateTimeOffset now, int windowDays)
    {
        if (current.IsLocal()
            && !string.Equals(current.LocalCountryCode, countryCode, StringComparison.OrdinalIgnoreCase))
        {
            return new ClaimCoverageCellDto(countryCode, ClaimCoverageStates.NotApplicable, null, null, null, null, false);
        }

        if (current.ActiveClosureFor(countryCode) is { } closure)
        {
            return new ClaimCoverageCellDto(
                countryCode, ClaimCoverageStates.Closed, null, null, null, closure.ReasonCode, false);
        }

        var version = CellVersion(versionsOfCode, countryCode);
        return version is null
            ? new ClaimCoverageCellDto(countryCode, ClaimCoverageStates.NotOpened, null, null, null, null, false)
            : new ClaimCoverageCellDto(countryCode, version.Status, version.Id, version.CountryVersion,
                version.BoundCoreVersion, null, IsExpiring(version, now, windowDays));
    }

    /// <summary>List-row summary: only the countries with something to say (a closure or a live version). The full
    /// grid (incl. not-opened / not-applicable) is the coverage endpoint.</summary>
    public static IReadOnlyList<ClaimCountrySummaryDto> Summary(Claim claim, IReadOnlyList<ClaimCountryVersion> versionsOfCode)
    {
        var countries = claim.CountryClosures.Where(c => c.ReopenedAt is null).Select(c => c.CountryCode)
            .Concat(versionsOfCode.Where(v => v.IsLive()).Select(v => v.CountryCode))
            .Select(ClaimV2Checks.NormalizeCountry)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal);

        return countries
            .Select(c => Cell(claim, c, versionsOfCode, DateTimeOffset.UtcNow, 0))
            .Where(c => c.State != ClaimCoverageStates.NotApplicable && c.State != ClaimCoverageStates.NotOpened)
            .Select(c => new ClaimCountrySummaryDto(c.CountryCode, c.State))
            .ToList();
    }
}
