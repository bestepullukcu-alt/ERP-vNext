using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using MediatR;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S6 — the third bridge moment: a task born from a decision inside the meeting's minutes
/// (<see cref="CreateTaskFromMeetingRequest.DecisionCode"/>). K4's source guard extends across this entry
/// point too: a decision inside an ALREADY-PUBLISHED version is never written to, even when it goes on to
/// produce a task — <see cref="A_task_from_a_decision_after_publish_never_touches_the_frozen_version"/> is
/// that proof, from the OTHER handler that could have broken it.
/// </summary>
public sealed class MinutesDecisionTaskBridgeTests
{
    private static readonly Guid MeetingId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();
    private const string CorrelationId = "corr";

    private sealed class BridgeMediator : IMediator
    {
        public Response<Guid> CreateTaskResult { get; set; } = Response<Guid>.Success(TaskId, 201, CorrelationId);

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is CreateTaskItemCommand)
            {
                return (Task<TResponse>)(object)Task.FromResult(CreateTaskResult);
            }

            throw new NotSupportedException($"not supported: {request.GetType().Name}");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class Fixture
    {
        public FakeMeetingRepository Meetings { get; } = new() { Tenant = TaskTestData.Tenant };
        public FakeMeetingTypeRepository Types { get; } = new() { Tenant = TaskTestData.Tenant };
        public FakeAgendaItemRepository AgendaItems { get; } = new() { Tenant = TaskTestData.Tenant };
        public FakeMeetingMinutesVersionRepository Minutes { get; } = new() { Tenant = TaskTestData.Tenant };
        public FakeRecordLinkRepository Links { get; } = new();
        public BridgeMediator Mediator { get; } = new();

        public CreateTaskFromMeetingHandler Handler()
        {
            var linkService = new RecordLinkService(Links, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
            return new CreateTaskFromMeetingHandler(
                Meetings, Types, AgendaItems, Minutes, linkService,
                new MeetingIdempotencyKeyResolver(), new FakeCurrentUserContext(TaskTestData.Me), Mediator);
        }

        public Meeting SeedMeeting()
        {
            var meeting = new Meeting
            {
                Id = MeetingId, TenantId = TaskTestData.Tenant, Title = "Toplantı", MeetingTypeId = Guid.NewGuid(),
                StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(-1).AddHours(1),
                OrganizerUserId = TaskTestData.Me, IdempotencyKey = "key", CreatedBy = "test"
            };
            Meetings.Seed(meeting);
            return meeting;
        }

        public void SeedMinutes(MinutesStatus status, params string[] decisionTexts)
        {
            Minutes.Seed(new MeetingMinutesVersion
            {
                TenantId = TaskTestData.Tenant,
                MeetingId = MeetingId,
                VersionNumber = 1,
                Status = status,
                Decisions = decisionTexts
                    .Select((text, i) => new MinutesDecision { Code = $"D-{i + 1}", Text = text })
                    .ToList(),
                CreatedBy = "test"
            });
        }
    }

    private static CreateTaskFromMeetingRequest Request(string decisionCode)
        => new("Karardan doğan görev", null, null, null, null, null, "idem-1", decisionCode);

    [Fact]
    public async Task A_task_from_a_Draft_decision_embeds_the_link_and_updates_ActionReferences()
    {
        var fx = new Fixture();
        fx.SeedMeeting();
        fx.SeedMinutes(MinutesStatus.Draft, "Bütçe artırılacak");

        var result = await fx.Handler().Handle(new CreateTaskFromMeetingCommand(MeetingId, Request("D-1"), CorrelationId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var link = Assert.Single(fx.Links.Items);
        Assert.Equal(RecordLinkTypes.BornFromMeeting, link.LinkType);
        Assert.False(link.CreatedAfterMinutesPublished);

        var stored = await fx.Minutes.GetLatestByMeetingIdAsync(MeetingId);
        var decision = Assert.Single(stored!.Decisions);
        Assert.Equal(link.Id, decision.RecordLinkId);
        Assert.Equal([link.Id], stored.ActionReferences);
    }

    [Fact]
    public async Task An_unknown_decision_code_is_404()
    {
        var fx = new Fixture();
        fx.SeedMeeting();
        fx.SeedMinutes(MinutesStatus.Draft, "Tek karar");

        var result = await fx.Handler().Handle(new CreateTaskFromMeetingCommand(MeetingId, Request("D-99"), CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.DecisionNotFound, result.ReasonCode);
        Assert.Empty(fx.Links.Items);
    }

    [Fact]
    public async Task A_decision_code_with_no_minutes_at_all_is_404()
    {
        var fx = new Fixture();
        fx.SeedMeeting();

        var result = await fx.Handler().Handle(new CreateTaskFromMeetingCommand(MeetingId, Request("D-1"), CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.DecisionNotFound, result.ReasonCode);
    }

    /// <summary>The source guard, from the OTHER handler that touches <c>MeetingMinutesVersion</c>: a task
    /// created "from" a decision that lives in an ALREADY-PUBLISHED version must flag the LINK
    /// (<c>CreatedAfterMinutesPublished</c>), and must NOT reach back into the frozen version to stamp a
    /// <c>RecordLinkId</c> onto that decision — the row published is the row that stays published.</summary>
    [Fact]
    public async Task A_task_from_a_decision_after_publish_never_touches_the_frozen_version()
    {
        var fx = new Fixture();
        fx.SeedMeeting();
        fx.SeedMinutes(MinutesStatus.Published, "Yayımlanmış karar");

        var result = await fx.Handler().Handle(new CreateTaskFromMeetingCommand(MeetingId, Request("D-1"), CorrelationId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var link = Assert.Single(fx.Links.Items);
        Assert.True(link.CreatedAfterMinutesPublished);

        var stored = await fx.Minutes.GetLatestByMeetingIdAsync(MeetingId);
        Assert.Equal(MinutesStatus.Published, stored!.Status);
        var decision = Assert.Single(stored.Decisions);
        Assert.Null(decision.RecordLinkId);
        Assert.Empty(stored.ActionReferences);
    }

    [Fact]
    public async Task An_ordinary_non_decision_task_after_publish_is_also_flagged_added_later()
    {
        var fx = new Fixture();
        fx.SeedMeeting();
        fx.SeedMinutes(MinutesStatus.Published, "Karar");

        var request = new CreateTaskFromMeetingRequest("Sıradan görev", null, null, null, null, null, "idem-2");
        var result = await fx.Handler().Handle(new CreateTaskFromMeetingCommand(MeetingId, request, CorrelationId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(Assert.Single(fx.Links.Items).CreatedAfterMinutesPublished);
    }
}
