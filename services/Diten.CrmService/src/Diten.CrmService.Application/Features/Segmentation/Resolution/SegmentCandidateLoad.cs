namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// The result of the single Phase-1 pushdown. <see cref="ExceededCap"/> is set when the query found MORE candidates
/// than the ceiling allows; the resolver then answers 422 and returns NOTHING. There is no silent truncation anywhere:
/// a partial member list is more dangerous than no list, because nobody can tell it is partial.
/// </summary>
public sealed record SegmentCandidateLoad(
    IReadOnlyList<SegmentSubjectSnapshot> Candidates,
    bool ExceededCap,
    int Cap);
