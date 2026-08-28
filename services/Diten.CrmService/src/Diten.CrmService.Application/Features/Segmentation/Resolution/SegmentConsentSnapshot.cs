using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// Everything the MOD-0164 evaluation engine needs for the WHOLE candidate set, fetched in two bulk reads. The engine
/// itself (<c>ConsentEvaluationEngine.Evaluate</c>) is a pure function of these lists, so the segment resolver reuses
/// the MOD-0164 decision logic verbatim without re-implementing it and without widening
/// <c>IConsentPreferenceEvaluator</c> — whose per-subject signature is exactly right for the 1x1 is-member path and
/// exactly wrong for a 10.000-candidate resolve.
/// </summary>
public sealed record SegmentConsentSnapshot(
    IReadOnlyList<ConsentRecord> Consents,
    IReadOnlyList<PreferenceRecord> Preferences);
