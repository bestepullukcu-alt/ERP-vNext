using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>
/// MOD-0280-FU01 v3 G2 — the person's correction of ONE captured (timer or meeting) row, sent by the save for each row it
/// actually changes. Its own audited command so every correction is its own audit entry: the week is the entity, and the
/// metadata carries the row, its source, the minutes before and after, and whether the note changed.
/// <para>BL-533 — <see cref="ExpectedVersion"/> is the row version the SAVE read (the row its person was looking at); the
/// correction is written only on that version. <see cref="Session"/> is the save's Platform transaction: the correction
/// lands with the save's claim and rows, or not at all.</para>
/// </summary>
public sealed record CorrectCapturedTimeEntryCommand(
    Guid WeekId, Guid EntryId, string Source, int BeforeMinutes, int AfterMinutes, bool NoteChanged, string? Note,
    int ExpectedVersion, string CorrelationId)
    : IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider
{
    public IPlatformTransactionSession? Session { get; init; }

    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Update, "TimesheetWeek",
        EntityId: WeekId, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        BeforeState: new Dictionary<string, object?> { ["entryId"] = EntryId, ["durationMinutes"] = BeforeMinutes },
        AfterState: new Dictionary<string, object?> { ["entryId"] = EntryId, ["durationMinutes"] = AfterMinutes, ["noteChanged"] = NoteChanged },
        Metadata: new Dictionary<string, object?>
        {
            ["transition"] = "correct-captured-row",
            ["entryId"] = EntryId,
            ["source"] = Source,
            ["beforeMinutes"] = BeforeMinutes,
            ["afterMinutes"] = AfterMinutes,
            ["noteChanged"] = NoteChanged
        });
}
