using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S12 (pack §23.7/§23.9 AC5) — the export's 50 000-row cap, REFUSED never trimmed.
///
/// <para><b>⚠ WHY THIS IS IN-MEMORY, NOT MONGO.</b> Proving the cap needs a report whose ONE dataset crosses
/// 50 000 rows; seeding 50 001 real documents into the shared test database on every run of this suite would
/// slow it by itself and put load on infrastructure BL-395 already flags as contended across concurrent
/// sessions. The visibility, draft-exclusion, carry-forward-attribution and overdue-at-query-time ACs
/// (<c>MeetingReportMongoTests</c>) are the ones this WP's own DOĞRULA names as requiring a real database; the
/// row-cap ARITHMETIC does not depend on Mongo at all — <c>MeetingReportCore.BuildAsync</c> runs unchanged over
/// whatever <see cref="IMeetingRepository.ListAsync"/> returns, fake or real.</para>
/// </summary>
public sealed class MeetingReportExportCapTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Organizer = Guid.NewGuid();

    [Fact]
    public async Task AC5_An_export_over_the_row_cap_is_refused_never_trimmed()
    {
        var meetings = new FakeMeetingRepository { Tenant = Tenant };
        var typeId = Guid.NewGuid();
        for (var i = 0; i < MeetingReportExportLimits.MaxRows + 1; i++)
        {
            meetings.Seed(new Meeting
            {
                Id = Guid.NewGuid(),
                TenantId = Tenant,
                Title = $"Meeting {i}",
                MeetingTypeId = typeId,
                StartAt = DateTimeOffset.UtcNow,
                EndAt = DateTimeOffset.UtcNow.AddHours(1),
                OrganizerUserId = Organizer,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                CreatedBy = "test"
            });
        }

        var handler = new ExportMeetingReportHandler(
            meetings,
            new FakeMeetingTypeRepository { Tenant = Tenant },
            new FakeMeetingAttendeeRepository { Tenant = Tenant },
            new FakeMeetingMinutesVersionRepository { Tenant = Tenant },
            new EmptyRecordLinkService(),
            new FakeTaskItemRepository(),
            new FakeCurrentUserContext(Organizer),
            new FakeActorPermissionContext(),
            new UnreachedExportAuditWriter());

        var response = await handler.Handle(
            new ExportMeetingReportQuery(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1),
                null, null, MeetingReportDatasets.Meetings, "csv", "en", "corr"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.ReportExportTooLarge, response.ReasonCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task Exactly_at_the_cap_is_allowed()
    {
        var meetings = new FakeMeetingRepository { Tenant = Tenant };
        var typeId = Guid.NewGuid();
        for (var i = 0; i < MeetingReportExportLimits.MaxRows; i++)
        {
            meetings.Seed(new Meeting
            {
                Id = Guid.NewGuid(),
                TenantId = Tenant,
                Title = $"Meeting {i}",
                MeetingTypeId = typeId,
                StartAt = DateTimeOffset.UtcNow,
                EndAt = DateTimeOffset.UtcNow.AddHours(1),
                OrganizerUserId = Organizer,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                CreatedBy = "test"
            });
        }

        var recordedRowCount = -1;
        var handler = new ExportMeetingReportHandler(
            meetings,
            new FakeMeetingTypeRepository { Tenant = Tenant },
            new FakeMeetingAttendeeRepository { Tenant = Tenant },
            new FakeMeetingMinutesVersionRepository { Tenant = Tenant },
            new EmptyRecordLinkService(),
            new FakeTaskItemRepository(),
            new FakeCurrentUserContext(Organizer),
            new FakeActorPermissionContext(),
            new CapturingExportAuditWriter(entry => recordedRowCount = entry.RowCount));

        var response = await handler.Handle(
            new ExportMeetingReportQuery(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1),
                null, null, MeetingReportDatasets.Meetings, "csv", "en", "corr"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful, string.Join("; ", response.Errors));
        Assert.Equal(MeetingReportExportLimits.MaxRows, response.Data!.RowCount);
        Assert.Equal(MeetingReportExportLimits.MaxRows, recordedRowCount);
    }

    private sealed class EmptyRecordLinkService : IRecordLinkService
    {
        public Task<RecordLink> AddLinkAsync(
            RecordLinkEndpoint source, RecordLinkEndpoint target, string linkType,
            string? idempotencyKey = null, bool createdAfterMinutesPublished = false, CancellationToken ct = default)
            => throw new NotSupportedException("Not exercised by the export cap test.");

        public Task RemoveLinkAsync(Guid linkId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<RecordLink?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
            => Task.FromResult<RecordLink?>(null);

        public Task<RecordLink?> FindLinkAsync(
            RecordLinkEndpoint source, RecordLinkEndpoint target, string linkType, CancellationToken ct = default)
            => Task.FromResult<RecordLink?>(null);

        public Task<IReadOnlyList<RecordLink>> ListBySourceAsync(
            IReadOnlyCollection<Guid> sourceRecordIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RecordLink>>([]);

        public Task<IReadOnlyList<RecordLink>> ListByTargetAsync(
            IReadOnlyCollection<Guid> targetRecordIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RecordLink>>([]);
    }

    /// <summary>The cap must be enforced BEFORE the audit write — if this is ever reached the refusal broke.</summary>
    private sealed class UnreachedExportAuditWriter : IDataExportAuditWriter
    {
        public Task<DataExportAuditResult> RecordAsync(DataExportAuditEntry entry, CancellationToken ct = default)
            => throw new InvalidOperationException("The audit writer must not be called when the export is refused for size.");
    }

    private sealed class CapturingExportAuditWriter(Action<DataExportAuditEntry> onRecord) : IDataExportAuditWriter
    {
        public Task<DataExportAuditResult> RecordAsync(DataExportAuditEntry entry, CancellationToken ct = default)
        {
            onRecord(entry);
            return Task.FromResult(new DataExportAuditResult(true, Domain.Enums.AuditActorType.TenantUser, null, null));
        }
    }
}
