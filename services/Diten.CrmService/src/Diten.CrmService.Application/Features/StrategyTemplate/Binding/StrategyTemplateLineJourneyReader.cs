using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Binding;

/// <summary>WP-SB-3a — what a reader is told about one product line's journey (never persisted).</summary>
public sealed record StrategyTemplateLineJourneyView(
    Guid? JourneyId,
    string? JourneyCode,
    string? JourneyName,
    string? JourneyStatus,
    bool JourneyMissing,
    IReadOnlyList<string> Warnings)
{
    /// <summary>A pre-SB-3a line: no journey yet (fixed with a new version, never backfilled).</summary>
    public static StrategyTemplateLineJourneyView Missing { get; } = new(null, null, null, null, true, Array.Empty<string>());
}

/// <summary>
/// WP-SB-3a (DESIGN-SB-3 §3.1) — the read side of a product line's journey: its code / name / status and the derived,
/// never-persisted hints (<see cref="StrategyProductLineJourneyWarnings"/>): no longer published, gone, or in a language
/// that is not a content language of the template's country scope. Hints are WARNINGS — an active play does not become
/// invalid because its journey moved on; the past must stay explainable.
/// <para>Strictly read-only: <c>GetByIdAsync</c> on the journey and one BRD read of <c>country-content-languages</c>
/// (only for a country-scoped template; an unreadable set simply yields no language hint, never a guess).</para>
/// </summary>
public sealed class StrategyTemplateLineJourneyReader
{
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly IReferenceDataCatalogReader? _catalog;

    public StrategyTemplateLineJourneyReader(
        IContentEngagementJourneyRepository journeys, IReferenceDataCatalogReader? catalog = null)
    {
        _journeys = journeys;
        _catalog = catalog;
    }

    public async Task<IReadOnlyDictionary<Guid, StrategyTemplateLineJourneyView>> ReadAsync(
        Guid tenantId, TemplateEntity template, CancellationToken cancellationToken)
    {
        var views = new Dictionary<Guid, StrategyTemplateLineJourneyView>();
        IReadOnlySet<string>? allowedLanguages = null;
        var languagesRead = false;

        foreach (var line in template.ProductLines)
        {
            if (line.JourneyId is not { } journeyId || journeyId == Guid.Empty)
            {
                views[line.LineId] = StrategyTemplateLineJourneyView.Missing;
                continue;
            }

            var journey = await _journeys.GetByIdAsync(tenantId, journeyId, cancellationToken);
            if (journey is null)
            {
                views[line.LineId] = new StrategyTemplateLineJourneyView(journeyId, line.JourneyCodeDisplay, null, null,
                    false, new[] { StrategyProductLineJourneyWarnings.NotFound });
                continue;
            }

            var warnings = new List<string>();
            if (journey.IsArchived() || !journey.IsPublished())
            {
                warnings.Add(StrategyProductLineJourneyWarnings.NotPublished);
            }

            if (!string.IsNullOrWhiteSpace(journey.LanguageCode))
            {
                if (!languagesRead)
                {
                    allowedLanguages = await CountryLanguagesAsync(template, cancellationToken);
                    languagesRead = true;
                }

                if (allowedLanguages is not null
                    && !allowedLanguages.Contains(ClaimV2Checks.NormalizeLanguage(journey.LanguageCode)))
                {
                    warnings.Add(StrategyProductLineJourneyWarnings.LanguageNotInCountry);
                }
            }

            views[line.LineId] = new StrategyTemplateLineJourneyView(journeyId, journey.JourneyCode, journey.JourneyName,
                journey.JourneyStatus, false, warnings);
        }

        return views;
    }

    /// <summary>The template's country scope's content languages, or null when the template is not country-scoped or
    /// the reference set cannot be read (no hint is better than a wrong one).</summary>
    private async Task<IReadOnlySet<string>?> CountryLanguagesAsync(TemplateEntity template, CancellationToken cancellationToken)
    {
        if (_catalog is null
            || !string.Equals(template.EffectiveScopeType(), StrategyTemplateScopeTypes.Country, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(template.CountryScope))
        {
            return null;
        }

        ReferenceSetSnapshot set;
        try
        {
            set = await _catalog.GetPublishedValuesAsync(ClaimReferenceSets.CountryContentLanguages, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (!set.IsPublished)
        {
            return null;
        }

        var country = ClaimV2Checks.NormalizeCountry(template.CountryScope);
        var row = set.Values.FirstOrDefault(v => v.IsActive && !v.IsDeprecated
            && string.Equals(ClaimV2Checks.NormalizeCountry(v.ValueCode), country, StringComparison.Ordinal));
        return row?.Attributes is { } attributes
               && attributes.TryGetValue(ClaimReferenceSets.LanguagesAttribute, out var raw)
            ? raw.Split(new[] { ',', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ClaimV2Checks.NormalizeLanguage).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>The product line summary: promo / non-promo lines (a pre-SB-3a line counts as promo) and lines without a
    /// journey.</summary>
    public static (int Promo, int NonPromo, int WithoutJourney) Summary(TemplateEntity template)
        => (template.ProductLines.Count(l => l.EffectiveRole() == StrategyProductLineRoles.Promo),
            template.ProductLines.Count(l => l.EffectiveRole() == StrategyProductLineRoles.NonPromo),
            template.ProductLines.Count(l => l.JourneyId is null || l.JourneyId == Guid.Empty));
}
