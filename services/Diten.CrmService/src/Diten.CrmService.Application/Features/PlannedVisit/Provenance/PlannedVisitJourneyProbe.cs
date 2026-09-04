using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.PlannedVisit.Provenance;

/// <summary>
/// MOD-0162 FU05 read-only OPTIONAL journey wrapper. Calls <see cref="IContentEngagementJourneyReader"/> <b>in-process
/// via DI</b> to (a) prove the chosen journey is <c>published</c> + effective (V17) and (b) prove the chosen stage
/// belongs to that journey's active, ordered stages (V18), then builds the <see cref="PlannedVisitContentRef"/>
/// content-position snapshot (D10).
/// <para>This is NOT an engine: it never advances a stage, evaluates a branch, or recommends a journey. <c>StageIndex</c>
/// is the stage's ordinal position on the resolved path, read for a future FU04 "next stage" — FU01 never increments it.
/// The journey's full stage content is never copied (D5); only the position marker and display snapshot are kept.</para>
/// </summary>
public sealed class PlannedVisitJourneyProbe
{
    private readonly IContentEngagementJourneyReader _reader;

    public PlannedVisitJourneyProbe(IContentEngagementJourneyReader reader) => _reader = reader;

    /// <summary>The probe's outcome: either a validation <see cref="PlannedVisitValidation.Failure"/>, or the built
    /// content ref (null when no journey was chosen — the binding is genuinely optional).</summary>
    public sealed record Result(PlannedVisitValidation.Failure? Failure, PlannedVisitContentRef? ContentRef);

    public async Task<Result> ResolveAsync(
        Guid? journeyId,
        Guid? stageId,
        string? contentSource,
        Guid? strategyTemplateId,
        CancellationToken cancellationToken)
    {
        // A ContentSource value, when supplied, is fail-closed vocabulary (AC-CONTENT-3).
        if (PlannedVisitValidation.Trim(contentSource) is { } cs
            && !PlannedVisitContentSource.IsKnown(cs))
        {
            return new Result(
                new PlannedVisitValidation.Failure(
                    $"Unsupported ContentSource '{cs}'.", PlannedVisitErrorCodes.UnsupportedVocabularyValue),
                null);
        }

        // No journey → no content ref. A stage without a journey is a contradiction (V18).
        if (journeyId is not { } jid || jid == Guid.Empty)
        {
            if (stageId is { } sid && sid != Guid.Empty)
            {
                return new Result(
                    new PlannedVisitValidation.Failure(
                        "A journey stage cannot be set without a journey.",
                        PlannedVisitErrorCodes.StageNotInJourney),
                    null);
            }

            return new Result(null, null);
        }

        var now = DateTimeOffset.UtcNow;
        var journeys = await _reader.ResolvePublishedJourneysAsync(
            new ContentEngagementJourneyCriteria(EffectiveAt: now), cancellationToken);
        var journey = journeys.FirstOrDefault(j => j.JourneyId == jid);
        if (journey is null)
        {
            return new Result(
                new PlannedVisitValidation.Failure(
                    "The selected journey is not published or not effective.",
                    PlannedVisitErrorCodes.JourneyNotPublished),
                null);
        }

        var normalizedSource = ResolveContentSource(contentSource);
        var contentRef = new PlannedVisitContentRef
        {
            JourneyId = jid,
            ContentSource = normalizedSource,
            IsOverridden = string.Equals(normalizedSource, PlannedVisitContentSource.Manual, StringComparison.Ordinal),
            StrategyTemplateId = strategyTemplateId,
            JourneyDisplayName = journey.JourneyName,
            ResolvedAt = now
        };

        if (stageId is { } stage && stage != Guid.Empty)
        {
            var stages = journey.Stages;
            var index = -1;
            for (var i = 0; i < stages.Count; i++)
            {
                if (stages[i].StageId == stage)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return new Result(
                    new PlannedVisitValidation.Failure(
                        "The selected stage does not belong to the chosen journey's active stages.",
                        PlannedVisitErrorCodes.StageNotInJourney),
                    null);
            }

            var chosen = stages[index];
            contentRef.StageId = stage;
            contentRef.StageIndex = index; // ordinal on the resolved path; FU01 never advances it
            contentRef.StageCode = chosen.StageCode;
            contentRef.StageDisplayName = chosen.StageName;
        }

        return new Result(null, contentRef);
    }

    /// <summary>Server-set marker (V27). FU01 has no strategy resolver (F-STRATEGY), so a rep-entered journey defaults to
    /// <c>manual</c>; a UI that default-filled 26/27 from a resolved strategy chain passes <c>strategy</c> explicitly.</summary>
    private static string ResolveContentSource(string? contentSource)
        => PlannedVisitValidation.Trim(contentSource) is { } cs && PlannedVisitContentSource.IsKnown(cs)
            ? PlannedVisitContentSource.Normalize(cs)
            : PlannedVisitContentSource.Manual;
}
