using System.Globalization;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;
using static Diten.Platform.Application.Tests.Audit.DataExportAuditTestKit;

namespace Diten.Platform.Application.Tests.Meetings;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// MOD-0357 S12 (pack §23) — the meeting report and its action register, on a REAL database. Every repository,
/// the record-link service, and (where marked) the real audit pipeline are production classes over MongoDB —
/// the same "nothing on the path is a double except what cannot run outside a host" discipline
/// <c>WorkReportExportAuditTrailMongoTests</c> already established for BL-347's OTHER caller.
///
/// AC references are to pack §23.9. Each `[Fact]` names the AC it proves in its own method name.
/// </summary>
public sealed class MeetingReportMongoTests : IAsyncLifetime
{
    private readonly Guid _organizer = Guid.NewGuid();
    private readonly Guid _attendee = Guid.NewGuid();
    private readonly Guid _outsider = Guid.NewGuid();
    private readonly Guid _readAllHolder = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();

    private MongoIntegrationHarness _harness = null!;
    private MeetingRepository _meetings = null!;
    private MeetingTypeRepository _types = null!;
    private MeetingAttendeeRepository _attendees = null!;
    private MeetingMinutesVersionRepository _minutesVersions = null!;
    private RecordLinkService _recordLinks = null!;
    private TaskItemRepository _tasks = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(
            SchemaProfile.Meetings, SchemaProfile.WorkflowWorkCenter, SchemaProfile.AccessGovernance);
        _meetings = new MeetingRepository(_harness.DbContext, _harness.TenantContext);
        _types = new MeetingTypeRepository(_harness.DbContext, _harness.TenantContext);
        _attendees = new MeetingAttendeeRepository(_harness.DbContext, _harness.TenantContext);
        _minutesVersions = new MeetingMinutesVersionRepository(_harness.DbContext, _harness.TenantContext);
        _recordLinks = new RecordLinkService(
            new RecordLinkRepository(_harness.DbContext, _harness.TenantContext),
            _harness.TenantContext,
            new FakeCurrentUserContext(_organizer));
        _tasks = new TaskItemRepository(
            _harness.DbContext, _harness.TenantContext, new TaskTransitionRepository(_harness.DbContext, _harness.TenantContext));
    }

    public async Task DisposeAsync()
    {
        var tenants = new[] { _harness.TenantId, _otherTenantId };
        await _harness.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings)
            .DeleteManyAsync(Builders<Meeting>.Filter.In(x => x.TenantId, tenants));
        await _harness.Database.GetCollection<MeetingAttendee>(PlatformCollections.MeetingAttendees)
            .DeleteManyAsync(Builders<MeetingAttendee>.Filter.In(x => x.TenantId, tenants));
        await _harness.Database.GetCollection<MeetingType>(PlatformCollections.MeetingTypes)
            .DeleteManyAsync(Builders<MeetingType>.Filter.In(x => x.TenantId, tenants));
        await _harness.Database.GetCollection<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions)
            .DeleteManyAsync(Builders<MeetingMinutesVersion>.Filter.In(x => x.TenantId, tenants));
        await _harness.Database.GetCollection<RecordLink>(PlatformCollections.MeetingRecordLinks)
            .DeleteManyAsync(Builders<RecordLink>.Filter.In(x => x.TenantId, tenants));
        await _harness.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems)
            .DeleteManyAsync(Builders<TaskItem>.Filter.In(x => x.TenantId, tenants));
        await _harness.DisposeAsync();
    }

    // ── AC1 — required period ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC1_An_inverted_or_equal_period_is_refused_before_any_query_runs()
    {
        var now = DateTimeOffset.UtcNow;
        var response = await Handler(_organizer).Handle(
            new GetMeetingReportQuery(now, now, null, null, "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.ReportInvalidPeriod, response.ReasonCode);
    }

    // ── AC2 — D3 visibility: organizer, attendee, unrelated third party, read-all holder ───────────────────────

    [Fact]
    public async Task AC2_Organizer_attendee_and_readAll_see_the_meeting_an_unrelated_third_party_sees_nothing()
    {
        var meeting = await SeedMeetingAsync();

        var organizerReport = await ReportAsync(_organizer);
        var attendeeReport = await ReportAsync(_attendee);
        var outsiderReport = await ReportAsync(_outsider);
        var readAllReport = await ReportAsync(_readAllHolder, hasReadAll: true);

        Assert.Contains(organizerReport.Meetings, m => m.Id == meeting.Id);
        Assert.Equal(MeetingReportDto.ScopeScoped, organizerReport.ScopeApplied);

        Assert.Contains(attendeeReport.Meetings, m => m.Id == meeting.Id);

        Assert.Empty(outsiderReport.Meetings);
        Assert.Equal(0, outsiderReport.Totals.MeetingCount);

        Assert.Contains(readAllReport.Meetings, m => m.Id == meeting.Id);
        Assert.Equal(MeetingReportDto.ScopeTenant, readAllReport.ScopeApplied);
    }

    [Fact]
    public async Task AC2_Another_tenants_context_sees_none_of_this_tenants_meetings()
    {
        await SeedMeetingAsync();

        MeetingReportDto report;
        _harness.TenantContext.SetTenant(_otherTenantId);
        try
        {
            report = await ReportAsync(_organizer, hasReadAll: true);
        }
        finally
        {
            _harness.TenantContext.SetTenant(_harness.TenantId);
        }

        Assert.Empty(report.Meetings);
    }

    // ── AC3 (decisions) — only PUBLISHED minutes feed the report (owner decision, §23.13/3) ─────────────────────

    [Fact]
    public async Task AC3_Only_published_minutes_decisions_reach_the_report_a_draft_versions_decisions_do_not()
    {
        var published = await SeedMeetingAsync(title: "Published-minutes meeting");
        await _minutesVersions.TryCreateAsync(new MeetingMinutesVersion
        {
            TenantId = _harness.TenantId,
            MeetingId = published.Id,
            VersionNumber = 1,
            Status = MinutesStatus.Published,
            PublishedAtUtc = DateTimeOffset.UtcNow,
            PublishedByUserId = _organizer,
            Decisions = [new MinutesDecision { Code = "D-1", Text = "Published decision" }],
            CreatedBy = "test"
        });

        var draft = await SeedMeetingAsync(title: "Draft-minutes meeting");
        await _minutesVersions.TryCreateAsync(new MeetingMinutesVersion
        {
            TenantId = _harness.TenantId,
            MeetingId = draft.Id,
            VersionNumber = 1,
            Status = MinutesStatus.Draft,
            Decisions = [new MinutesDecision { Code = "D-1", Text = "Draft-only decision" }],
            CreatedBy = "test"
        });

        var report = await ReportAsync(_organizer);

        Assert.Contains(report.Decisions, d => d.MeetingId == published.Id && d.Text == "Published decision");
        Assert.DoesNotContain(report.Decisions, d => d.MeetingId == draft.Id);
    }

    // ── AC3 (actions) — origin attribution survives two S7-style carry-forwards ─────────────────────────────────

    [Fact]
    public async Task AC3_An_action_carried_through_two_continuations_is_attributed_once_to_its_ORIGIN_meeting()
    {
        var origin = await SeedMeetingAsync(title: "Origin meeting");
        var task = await _tasks.CreateAsync(new TaskItem
        {
            TenantId = _harness.TenantId,
            OrganizationUnitId = Guid.NewGuid(),
            Title = "Action born in the origin meeting",
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = _organizer,
            CreatedByUserId = _organizer,
            Lifecycle = TaskLifecycle.Open
        });
        await _recordLinks.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, origin.Id),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, task.Id),
            RecordLinkTypes.BornFromMeeting,
            ct: CancellationToken.None);

        // First continuation — the S7 carry-forward shape (Source=meeting, Target=task, LinkType=Agenda),
        // written the exact way MeetingFollowUpCommandHandlers.cs itself writes it.
        var continuation1 = await SeedMeetingAsync(title: "Continuation 1", startAt: DateTimeOffset.UtcNow.AddDays(1));
        await _recordLinks.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, continuation1.Id),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, task.Id),
            RecordLinkTypes.Agenda,
            ct: CancellationToken.None);

        // Second continuation — carried forward again; this is the CURRENT meeting.
        var continuation2 = await SeedMeetingAsync(title: "Continuation 2", startAt: DateTimeOffset.UtcNow.AddDays(2));
        await _recordLinks.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, continuation2.Id),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, task.Id),
            RecordLinkTypes.Agenda,
            ct: CancellationToken.None);

        var report = await ReportAsync(_organizer, from: DateTimeOffset.UtcNow.AddDays(-1), to: DateTimeOffset.UtcNow.AddDays(3));

        var row = Assert.Single(report.Actions, a => a.TaskId == task.Id);
        Assert.Equal(origin.Id, row.OriginMeetingId);
        Assert.Equal("Origin meeting", row.OriginMeetingTitle);
        Assert.Equal(continuation2.Id, row.CurrentMeetingId);
        Assert.Equal("Continuation 2", row.CurrentMeetingTitle);
    }

    // ── AC4 — overdue is anchored to QUERY TIME, never to the filter period's own end ───────────────────────────

    [Fact]
    public async Task AC4_A_future_due_date_inside_the_filter_period_is_NOT_overdue_a_past_one_IS()
    {
        var meeting = await SeedMeetingAsync();

        var overdueTask = await _tasks.CreateAsync(new TaskItem
        {
            TenantId = _harness.TenantId,
            OrganizationUnitId = Guid.NewGuid(),
            Title = "Already past its due date",
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = _organizer,
            CreatedByUserId = _organizer,
            Lifecycle = TaskLifecycle.Open,
            DueAt = DateTimeOffset.UtcNow.AddHours(-2)
        });
        await _recordLinks.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, meeting.Id),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, overdueTask.Id),
            RecordLinkTypes.BornFromMeeting,
            ct: CancellationToken.None);

        // Due date is inside a period that extends a MONTH past it — a period-end-anchored implementation
        // would call this late (DueAt < filter.To); the query-time rule must not.
        var notYetDueTask = await _tasks.CreateAsync(new TaskItem
        {
            TenantId = _harness.TenantId,
            OrganizationUnitId = Guid.NewGuid(),
            Title = "Due tomorrow, not late",
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = _organizer,
            CreatedByUserId = _organizer,
            Lifecycle = TaskLifecycle.Open,
            DueAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        await _recordLinks.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, meeting.Id),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, notYetDueTask.Id),
            RecordLinkTypes.BornFromMeeting,
            ct: CancellationToken.None);

        var report = await ReportAsync(_organizer, from: DateTimeOffset.UtcNow.AddDays(-1), to: DateTimeOffset.UtcNow.AddDays(30));

        Assert.True(Assert.Single(report.Actions, a => a.TaskId == overdueTask.Id).IsOverdue);
        Assert.False(Assert.Single(report.Actions, a => a.TaskId == notYetDueTask.Id).IsOverdue);
        Assert.Equal(1, report.Totals.OverdueActionCount);
    }

    // ── export row cap — in-memory fakes (not Mongo): seeding 50 001 real documents is impractical inside a
    // shared-database test suite; the boundary check below exercises the SAME handler code path the Mongo
    // tests above exercise, over a synthetic in-memory report shape. ────────────────────────────────────────

    // (see MeetingReportExportCapTests for AC5)

    // ── audit trail (BL-347) — the real pipeline, same shape WorkReportExportAuditTrailMongoTests already
    // proves for its own caller. ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_leaves_exactly_one_tenant_owned_DataExport_record_ActorType_TenantUser()
    {
        var meeting = await SeedMeetingAsync();

        var principal = Principal("tenant_user", _organizer, tenantClaim: _harness.TenantId);
        var recorder = new RecordingAuditService(RealAuditService(principal));

        var response = await ExportHandler(principal, recorder).Handle(
            new ExportMeetingReportQuery(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1),
                null, null, MeetingReportDatasets.Meetings, "csv", "tr", "corr-s12-export"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful, string.Join("; ", response.Errors));
        Assert.Equal(1, response.Data!.RowCount);
        Assert.Contains("toplanti-raporu-meetings_", response.Data.FileName, StringComparison.Ordinal);

        var request = Assert.Single(recorder.Requests);
        Assert.False(request.IsPlatformGlobal);
        Assert.False(request.IsMetaAudit);
        Assert.Equal(AuditAppendStatus.Queued, Assert.Single(recorder.Results).Status);

        var record = Assert.Single(await Outbox(_harness.Database).Find(m => m.TenantId == _harness.TenantId).ToListAsync());
        Assert.Equal("Meetings.ExportMeetingReportQuery", record.RequestType);
        Assert.Equal(AuditOperation.Export, record.Operation);

        var written = new AuditOutboxPayloadMapper().Map(ToProcessingItem(record), DateTimeOffset.UtcNow);
        Assert.Equal(AuditActorType.TenantUser, written.ActorType);
        Assert.Equal(_organizer, written.ActorId);
        Assert.Equal(_harness.TenantId, written.TenantId);
        Assert.Equal(_harness.TenantId, written.TargetTenantId);
        Assert.Equal(AuditCategory.DataExport, written.Category);
        Assert.Equal("MeetingReport", written.EntityType);
        Assert.Equal("MOD-0357", written.SourceModule);
        Assert.False(written.IsMetaAudit);
        Assert.Equal("meetings", written.Metadata["dataset"]);
        Assert.Equal(1, Convert.ToInt32(written.Metadata["rowCount"], CultureInfo.InvariantCulture));

        _ = meeting;
    }

    [Fact]
    public async Task When_the_audit_append_fails_the_export_is_refused_with_503_and_no_file_content_is_returned()
    {
        await SeedMeetingAsync();
        var principal = Principal("tenant_user", _organizer, tenantClaim: _harness.TenantId);
        var unreachable = new AuditService(
            new UnreachableOutbox(),
            new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()),
            new AuditIdempotencyKeyBuilder(),
            new AuditRecursionGuard(),
            _harness.TenantContext,
            new CurrentUserContext(principal),
            NullLogger<AuditService>.Instance);

        var response = await ExportHandler(principal, unreachable).Handle(
            new ExportMeetingReportQuery(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1),
                null, null, MeetingReportDatasets.Meetings, "csv", "en", "corr-s12-down"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal(DataExportAuditReasonCodes.NotRecorded, response.ReasonCode);
        Assert.Null(response.Data);
        Assert.Equal(0, await Outbox(_harness.Database).CountDocumentsAsync(m => m.TenantId == _harness.TenantId));
    }

    // ── seed / harness helpers ──────────────────────────────────────────────────────────────────────────────

    private async Task<Meeting> SeedMeetingAsync(string title = "S12 report meeting", DateTimeOffset? startAt = null)
    {
        var type = await _types.CreateAsync(new MeetingType
        {
            TenantId = _harness.TenantId, Name = "S12-" + Guid.NewGuid(), CreatedBy = "test"
        });
        var start = startAt ?? DateTimeOffset.UtcNow;
        var meeting = await _meetings.CreateAsync(new Meeting
        {
            TenantId = _harness.TenantId,
            Title = title,
            MeetingTypeId = type.Id,
            StartAt = start,
            EndAt = start.AddHours(1),
            OrganizerUserId = _organizer,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CreatedBy = "test"
        });
        await _attendees.CreateAsync(new MeetingAttendee
        {
            TenantId = _harness.TenantId, MeetingId = meeting.Id, UserId = _organizer,
            InvitationResponse = InvitationResponse.Accepted, CreatedBy = "test"
        });
        await _attendees.CreateAsync(new MeetingAttendee
        {
            TenantId = _harness.TenantId, MeetingId = meeting.Id, UserId = _attendee,
            InvitationResponse = InvitationResponse.Pending, CreatedBy = "test"
        });
        return meeting;
    }

    private GetMeetingReportHandler Handler(Guid callerId, bool hasReadAll = false) => new(
        _meetings, _types, _attendees, _minutesVersions, _recordLinks, _tasks,
        new FakeCurrentUserContext(callerId),
        new FakeActorPermissionContext(hasReadAll ? [MeetingPermissions.ReadAll] : []));

    private async Task<MeetingReportDto> ReportAsync(
        Guid callerId, bool hasReadAll = false, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var response = await Handler(callerId, hasReadAll).Handle(
            new GetMeetingReportQuery(
                from ?? DateTimeOffset.UtcNow.AddDays(-1), to ?? DateTimeOffset.UtcNow.AddDays(1),
                null, null, "corr"),
            CancellationToken.None);
        Assert.True(response.IsSuccessful, string.Join("; ", response.Errors));
        return response.Data!;
    }

    private ExportMeetingReportHandler ExportHandler(IHttpContextAccessor principal, IAuditService audit)
    {
        var currentUser = new CurrentUserContext(principal);
        var exportAudit = new DataExportAuditWriter(
            audit,
            new JwtTenantAuthorizationContext(principal, new FakeDataScopeResolver()),
            currentUser,
            _harness.TenantContext,
            NullLogger<DataExportAuditWriter>.Instance);

        return new ExportMeetingReportHandler(
            _meetings, _types, _attendees, _minutesVersions, _recordLinks, _tasks,
            currentUser, new FakeActorPermissionContext(), exportAudit);
    }

    private AuditService RealAuditService(IHttpContextAccessor principal) => new(
        new AuditOutboxRepository(_harness.DbContext),
        new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()),
        new AuditIdempotencyKeyBuilder(),
        new AuditRecursionGuard(),
        _harness.TenantContext,
        new CurrentUserContext(principal),
        NullLogger<AuditService>.Instance);

    private sealed class UnreachableOutbox : IAuditOutboxWriter
    {
        public Task<bool> TryEnqueueAsync(AuditOutboxWriteRequest request, CancellationToken ct = default)
            => throw new TimeoutException("test: the audit outbox is unreachable");
    }
}
