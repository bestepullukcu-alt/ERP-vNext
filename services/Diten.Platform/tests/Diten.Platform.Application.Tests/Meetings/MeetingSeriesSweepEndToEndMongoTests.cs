using System.Text;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.BackgroundJobs;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// Go-live, 2026-09-13 — scenario F end to end. <see cref="MeetingSeriesSweepJobTests"/> proves the job walks
/// every tenant against a mediator double, and <see cref="GenerateDueMeetingSeriesHandlerTests"/> proves the
/// handler's dispatch against a recording mediator; neither lets the generated meeting actually be written, so
/// neither can say who ends up on it or who gets the invitation. Here the REAL <see cref="MeetingSeriesSweepJob"/>
/// drives the REAL <see cref="GenerateDueMeetingSeriesHandler"/>, which drives the REAL
/// <see cref="CreateMeetingHandler"/> over the REAL Mongo repositories and the REAL <see cref="MeetingInviteMailer"/>.
///
/// <para>The job runs with no user behind it, exactly as in production: the create handler's current user is
/// <see cref="Guid.Empty"/> (what <c>CurrentUserContext</c> answers with no HTTP context).</para>
/// </summary>
public sealed class MeetingSeriesSweepEndToEndMongoTests : IAsyncLifetime
{
    private const string InviteEvent = "platform.meetings.invite";
    private const string OrganizerAddedEvent = "platform.meetings.organizer-added";

    private readonly Guid _organizer = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    private MongoIntegrationHarness _harness = null!;
    private MeetingRepository _meetings = null!;
    private MeetingTypeRepository _types = null!;
    private MeetingAttendeeRepository _attendees = null!;
    private MeetingSeriesRepository _series = null!;
    private AgendaItemRepository _agenda = null!;
    private RecordLinkRepository _links = null!;
    private readonly ITestOutputHelper _output;

    public MeetingSeriesSweepEndToEndMongoTests(ITestOutputHelper output) => _output = output;
    private readonly RecordingDispatchAdapter _dispatches = new();
    private readonly FakeEligibilityMediator _eligibility = new();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _meetings = new MeetingRepository(_harness.DbContext, _harness.TenantContext);
        _types = new MeetingTypeRepository(_harness.DbContext, _harness.TenantContext);
        _attendees = new MeetingAttendeeRepository(_harness.DbContext, _harness.TenantContext);
        _series = new MeetingSeriesRepository(_harness.DbContext, _harness.TenantContext);
        _agenda = new AgendaItemRepository(_harness.DbContext, _harness.TenantContext);
        _links = new RecordLinkRepository(_harness.DbContext, _harness.TenantContext);
        _eligibility.EligibleUserIds.UnionWith([_organizer, _alice, _bob]);
    }

    public async Task DisposeAsync()
    {
        var tenant = _harness.TenantId;
        await _harness.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<MeetingAttendee>(PlatformCollections.MeetingAttendees)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<MeetingType>(PlatformCollections.MeetingTypes)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<MeetingSeries>(PlatformCollections.MeetingSeries)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<AgendaItem>(PlatformCollections.MeetingAgendaItems)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<RecordLink>(PlatformCollections.MeetingRecordLinks)
            .DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task The_sweep_creates_the_next_occurrence_with_the_rules_organizer_and_participants_and_invites_those_participants()
    {
        var rule = await SeedDueSeriesAsync();

        await RunSweepAsync();

        var meeting = Assert.Single(await _meetings.ListAsync());
        Assert.Equal(rule.Name, meeting.Title);
        Assert.Equal(_organizer, meeting.OrganizerUserId);
        Assert.Equal(rule.StartsAt, meeting.StartAt);

        var attendees = await _attendees.ListByMeetingIdAsync(meeting.Id);
        Assert.Equal(
            new[] { _organizer, _alice, _bob }.Order().ToArray(),
            attendees.Select(a => a.UserId).Order().ToArray());
        Assert.Equal(InvitationResponse.Accepted, attendees.Single(a => a.UserId == _organizer).InvitationResponse);
        Assert.Equal(InvitationResponse.Pending, attendees.Single(a => a.UserId == _alice).InvitationResponse);
        Assert.Equal(InvitationResponse.Pending, attendees.Single(a => a.UserId == _bob).InvitationResponse);

        // BL-373/BL-387 (owner, 2026-09-14) — the sweep acts as Guid.Empty, so the actor rule excludes nobody, and
        // the organizer is now split into their OWN "added to your calendar" mail rather than the plain invite
        // Alice and Bob get — never left out entirely (the rejected BL-387 patch), since the .ics is the only way
        // this meeting reaches the organizer's own calendar.
        var invite = Assert.Single(_dispatches.Requests, r => r.EventCode == InviteEvent);
        var recipients = invite.To.Select(r => r.Email).ToList();
        _output.WriteLine(
            $"sweep invite recipients={recipients.Count} alice={recipients.Contains(EmailOf(_alice))} " +
            $"bob={recipients.Contains(EmailOf(_bob))} organizerAlsoMailed={recipients.Contains(EmailOf(_organizer))}");
        Assert.Contains(EmailOf(_alice), recipients);
        Assert.Contains(EmailOf(_bob), recipients);
        Assert.DoesNotContain(EmailOf(_organizer), recipients);
        var ics = Encoding.UTF8.GetString(Assert.Single(invite.Attachments!).Content).Replace("\r\n ", string.Empty);
        Assert.Contains($"UID:{meeting.Id}@diten", ics);

        var organizerAdded = Assert.Single(_dispatches.Requests, r => r.EventCode == OrganizerAddedEvent);
        Assert.Equal([EmailOf(_organizer)], organizerAdded.To.Select(r => r.Email).ToArray());
        var organizerIcs = Encoding.UTF8.GetString(Assert.Single(organizerAdded.Attachments!).Content).Replace("\r\n ", string.Empty);
        Assert.Contains($"UID:{meeting.Id}@diten", organizerIcs);

        Assert.Equal(meeting.Id, (await _series.GetByIdAsync(rule.Id))!.LastGeneratedMeetingId);
    }

    [Fact]
    public async Task Running_the_sweep_twice_creates_the_occurrence_once_and_sends_the_invitation_once()
    {
        var rule = await SeedDueSeriesAsync();

        await RunSweepAsync();
        await RunSweepAsync();

        var meeting = Assert.Single(await _meetings.ListAsync());
        Assert.Single(_dispatches.Requests, r => r.EventCode == InviteEvent);
        Assert.Single(_dispatches.Requests, r => r.EventCode == OrganizerAddedEvent);
        Assert.Equal(meeting.Id, (await _series.GetByIdAsync(rule.Id))!.LastGeneratedMeetingId);
    }

    // ── harness ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Its first occurrence starts in two days, inside a three-day lead time; the second is a week later
    /// and outside it — so one sweep pass owes exactly one meeting, and a second pass owes nothing new.</summary>
    private async Task<MeetingSeries> SeedDueSeriesAsync()
    {
        var type = await _types.CreateAsync(new MeetingType
        {
            TenantId = _harness.TenantId, Name = "MGMT-REVIEW " + Guid.NewGuid(), CreatedBy = "test"
        });
        var anchor = new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(2).UtcDateTime.Date.AddHours(9), TimeSpan.Zero);

        return await _series.CreateAsync(new MeetingSeries
        {
            TenantId = _harness.TenantId,
            Name = "Haftalık Kalite " + Guid.NewGuid().ToString("N")[..6],
            MeetingTypeId = type.Id,
            Frequency = MeetingSeriesFrequency.Weekly,
            Interval = 1,
            StartsAt = anchor,
            DurationMinutes = 60,
            Location = "Room 2",
            OrganizerUserId = _organizer,
            AttendeeUserIds = [_alice, _bob],
            LeadTimeDays = 3,
            ChainAsFollowUp = true,
            IsActive = true,
            CreatedBy = "test"
        });
    }

    private Task RunSweepAsync()
    {
        var job = new MeetingSeriesSweepJob(
            new SingleTenantRegistry(_harness.TenantId), _harness.TenantContext, new SweepMediator(this),
            NullLogger<MeetingSeriesSweepJob>.Instance);
        return job.HandleAsync(
            new MeetingSeriesSweepJobArgs(100),
            new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Recurring, TriggeredBy: "test"),
            CancellationToken.None);
    }

    private static string EmailOf(Guid userId) => $"{userId:N}@example.test";

    /// <summary>MediatR's job in production: routes each command to its REAL handler. Only the eligibility lookup
    /// is answered by the double, the same seam every other meeting handler test doubles.</summary>
    private sealed class SweepMediator(MeetingSeriesSweepEndToEndMongoTests host) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            object result = request switch
            {
                GenerateDueMeetingSeriesCommand cmd => new GenerateDueMeetingSeriesHandler(
                    host._series, this, NullLogger<GenerateDueMeetingSeriesHandler>.Instance).Handle(cmd, ct),
                CreateMeetingCommand cmd => new CreateMeetingHandler(
                    host._meetings, host._types, host._attendees, host._harness.TenantContext,
                    new FakeCurrentUserContext(Guid.Empty), new MeetingIdempotencyKeyResolver(), host._eligibility,
                    new MeetingInviteMailer(
                        host._dispatches, new EmailPerUserResolver(), host._harness.TenantContext,
                        Options.Create(new AuthServiceOptions()), NullLogger<MeetingInviteMailer>.Instance)).Handle(cmd, ct),
                // KS1 — a chained series' later instance goes through the follow-up command, so it is routed to the
                // REAL handler. Left unrouted, GenerateDueMeetingSeriesHandler's own per-series catch swallowed the
                // NotSupportedException and hid a duplicate occurrence (measured under sabotage, 2026-09-13). These
                // meetings carry no task link, so the carry-forward never reads tasks — an empty double stands in.
                ScheduleFollowUpMeetingCommand cmd => new ScheduleFollowUpMeetingHandler(
                    host._meetings, host._agenda, new FakeTaskItemRepository(),
                    new RecordLinkService(host._links, host._harness.TenantContext, new FakeCurrentUserContext(Guid.Empty)),
                    host._harness.TenantContext, new MeetingIdempotencyKeyResolver(), new FakeCurrentUserContext(Guid.Empty),
                    this).Handle(cmd, ct),
                GetTaskAssignmentPersonLookupQuery query => host._eligibility.Send(query, ct),
                _ => throw new NotSupportedException($"SweepMediator does not route {request.GetType().Name}.")
            };
            return (Task<TResponse>)result;
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }

    private sealed class RecordingDispatchAdapter : INotificationEventDispatchAdapter
    {
        public List<NotificationEventDispatchRequest> Requests { get; } = [];

        public Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
            NotificationEventDispatchRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(Response<NotificationDispatchDto>.Success());
        }
    }

    private sealed class EmailPerUserResolver : ITaskNotificationRecipientResolver
    {
        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>(
                userIds.Select(id => new TaskNotificationRecipient(id, EmailOf(id), "User " + id.ToString("N")[..4])).ToList());
    }

    private sealed class SingleTenantRegistry(Guid tenantId) : ITenantRegistryRepository
    {
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>([
                new Tenant
                {
                    Id = tenantId,
                    Code = $"T-{tenantId:N}"[..10],
                    Slug = $"t-{tenantId:N}"[..10],
                    Name = "Tenant",
                    DisplayName = "Tenant",
                    Domain = $"{tenantId:N}.example",
                    Status = TenantStatus.Active
                }
            ]);

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
