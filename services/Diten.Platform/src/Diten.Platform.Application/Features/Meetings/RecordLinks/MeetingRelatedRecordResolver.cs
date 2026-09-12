using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Meetings.RecordLinks;

/// <summary>
/// MOD-0357 S2 — resolves a <c>RecordLink</c>'s "meetings"-side id into a title/link, the twin of
/// <see cref="TaskRelatedRecordResolver"/>. The deep link is <c>/Meetings/Details/{id}</c> — S3's own route
/// (pack §9), fixed as a constant here even though S3 has not built the screen yet: the WP's own NE #5 names
/// this exact route.
/// </summary>
public sealed class MeetingRelatedRecordResolver : IRelatedRecordResolver
{
    private readonly IMeetingRepository _meetings;

    public MeetingRelatedRecordResolver(IMeetingRepository meetings) => _meetings = meetings;

    public string ModuleCode => RecordLinkModuleCodes.Meetings;

    public async Task<IReadOnlyDictionary<Guid, RelatedRecordSummary>> ResolveAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, RelatedRecordSummary>();
        }

        var meetings = await _meetings.ListByIdsAsync(ids, ct);
        return meetings.ToDictionary(
            meeting => meeting.Id,
            meeting => new RelatedRecordSummary(meeting.Title, $"/Meetings/Details/{meeting.Id}"));
    }
}
