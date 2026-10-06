using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S11 — <see cref="GenerateDueMeetingSeriesHandler"/>: KS1 (no third creation path — first instance via
/// CreateMeetingCommand, later ones via ScheduleFollowUpMeetingCommand), KS2 (the claim makes two overlapping
/// sweep passes produce ONE meeting), KS3 (the organizer is the series', never the caller's — this handler runs
/// with no user context, exactly like the sweep itself), KS5 (window/EndsAt/IsActive).
/// </summary>
public sealed class GenerateDueMeetingSeriesHandlerTests
{
    private const string CorrelationId = "corr-1";
    private static readonly Guid TypeId = Guid.Parse("33333333-0000-0000-0000-000000000003");
    private static readonly Guid SeriesOrganizer = Guid.Parse("77777777-0000-0000-0000-000000000007");
    private static readonly DateTimeOffset StartsAt = new(2027, 2, 1, 9, 0, 0, TimeSpan.Zero);

    private static MeetingSeries NewSeries(
        DateTimeOffset? lastGeneratedAt = null, Guid? lastGeneratedMeetingId = null,
        bool chainAsFollowUp = true, bool isActive = true, DateTimeOffset? endsAt = null,
        string? lastProcessInstanceId = null, string name = "Haftalık Kalite") => new()
    {
        Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Name = name, MeetingTypeId = TypeId,
        Frequency = MeetingSeriesFrequency.Weekly, Interval = 1, StartsAt = StartsAt, EndsAt = endsAt,
        DurationMinutes = 60, OrganizerUserId = SeriesOrganizer, LeadTimeDays = 14,
        ChainAsFollowUp = chainAsFollowUp, IsActive = isActive, LastGeneratedAt = lastGeneratedAt,
        LastGeneratedMeetingId = lastGeneratedMeetingId, LastProcessInstanceId = lastProcessInstanceId,
        CreatedBy = "test"
    };

    private static (GenerateDueMeetingSeriesHandler Handler, FakeMeetingSeriesRepository Series, RecordingMediator Mediator)
        Build(params MeetingSeries[] seed)
    {
        var series = new FakeMeetingSeriesRepository { Tenant = TaskTestData.Tenant };
        foreach (var s in seed) { series.Seed(s); }
        var mediator = new RecordingMediator();
        var handler = new GenerateDueMeetingSeriesHandler(series, mediator, NullLogger<GenerateDueMeetingSeriesHandler>.Instance);
        return (handler, series, mediator);
    }

    [Fact]
    public async Task KS1_the_first_instance_is_created_via_the_ordinary_CreateMeetingCommand()
    {
        var series = NewSeries();
        var (handler, seriesRepo, mediator) = Build(series);
        var newMeetingId = Guid.NewGuid();
        mediator.CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(newMeetingId), 201, CorrelationId);

        var result = await handler.Handle(
            new GenerateDueMeetingSeriesCommand(StartsAt, MaxSeries: 10, CorrelationId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.MeetingsGenerated);
        Assert.True(mediator.CreateMeetingWasSent);
        Assert.False(mediator.ScheduleFollowUpWasSent);
        var stored = await seriesRepo.GetByIdAsync(series.Id, CancellationToken.None);
        Assert.Equal(newMeetingId, stored!.LastGeneratedMeetingId);
        Assert.Equal(StartsAt, stored.LastGeneratedAt);
    }

    [Fact]
    public async Task KS1_a_later_instance_is_chained_via_ScheduleFollowUpMeetingCommand_when_ChainAsFollowUp_is_true()
    {
        var sourceMeetingId = Guid.NewGuid();
        var series = NewSeries(lastGeneratedAt: StartsAt, lastGeneratedMeetingId: sourceMeetingId);
        var (handler, _, mediator) = Build(series);
        mediator.ScheduleFollowUpResult = Response<ScheduleFollowUpMeetingResultDto>.Success(
            new ScheduleFollowUpMeetingResultDto(Guid.NewGuid(), 2), 201, CorrelationId);

        var result = await handler.Handle(
            new GenerateDueMeetingSeriesCommand(StartsAt.AddDays(7), MaxSeries: 10, CorrelationId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.MeetingsGenerated);
        Assert.False(mediator.CreateMeetingWasSent);
        Assert.True(mediator.ScheduleFollowUpWasSent);
        Assert.Equal(sourceMeetingId, mediator.LastFollowUpSourceId);
    }

    [Fact]
    public async Task KS1_a_later_instance_uses_a_plain_create_when_ChainAsFollowUp_is_false()
    {
        var series = NewSeries(lastGeneratedAt: StartsAt, lastGeneratedMeetingId: Guid.NewGuid(), chainAsFollowUp: false);
        var (handler, _, mediator) = Build(series);
        mediator.CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(Guid.NewGuid()), 201, CorrelationId);

        await handler.Handle(
            new GenerateDueMeetingSeriesCommand(StartsAt.AddDays(7), MaxSeries: 10, CorrelationId), CancellationToken.None);

        Assert.True(mediator.CreateMeetingWasSent);
        Assert.False(mediator.ScheduleFollowUpWasSent);
    }

    [Fact]
    public async Task KS3_the_organizer_is_the_series_own_even_though_this_handler_has_no_caller()
    {
        var series = NewSeries();
        var (handler, _, mediator) = Build(series);
        mediator.CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(Guid.NewGuid()), 201, CorrelationId);

        await handler.Handle(
            new GenerateDueMeetingSeriesCommand(StartsAt, MaxSeries: 10, CorrelationId), CancellationToken.None);

        // No ICurrentUserContext is even injected into this handler — the only way the organizer could be
        // anything but the series' own value is if this handler forwarded some OTHER value. It never does.
        Assert.Equal(SeriesOrganizer, mediator.LastCreateMeetingRequest!.OrganizerUserId);
    }

    [Fact]
    public async Task KS2_an_occurrence_already_stamped_as_claimed_is_skipped_not_regenerated()
    {
        // The claim (LastProcessInstanceId) already names THIS occurrence — the short-circuit a sweep pass
        // that raced a moment behind another (both computed the same occurrence, the other's claim write won)
        // relies on, so the loser never reaches the create call at all.
        var series = NewSeries();
        series.LastProcessInstanceId = MeetingSeriesSchedule.ProcessInstanceId(series.Id, StartsAt);
        var (handler, _, mediator) = Build(series);

        var result = await handler.Handle(
            new GenerateDueMeetingSeriesCommand(StartsAt, MaxSeries: 10, CorrelationId), CancellationToken.None);

        Assert.Equal(0, result.Data!.MeetingsGenerated);
        Assert.Equal(1, result.Data.AlreadyGenerated);
        Assert.False(mediator.CreateMeetingWasSent);
    }

    [Fact]
    public async Task KS2_the_claim_is_an_expected_version_write_a_stale_second_claim_loses()
    {
        // The actual race: two sweep passes both read the series BEFORE either writes, so both hold the SAME
        // starting Version. The FIRST claim (in this handler's own successful run) bumps the stored Version;
        // the SECOND caller's own stale copy — captured before that run — can no longer win the same write,
        // which is the literal mechanism "two overlapping sweeps produce one meeting" rests on.
        var series = NewSeries();
        var staleVersion = series.Version;
        var (handler, seriesRepo, mediator) = Build(series);
        mediator.CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(Guid.NewGuid()), 201, CorrelationId);

        await handler.Handle(new GenerateDueMeetingSeriesCommand(StartsAt, MaxSeries: 10, CorrelationId), CancellationToken.None);

        var staleClaimAttempt = await seriesRepo.UpdateAsync(
            new MeetingSeries
            {
                Id = series.Id, TenantId = TaskTestData.Tenant, Name = series.Name, MeetingTypeId = TypeId,
                Frequency = MeetingSeriesFrequency.Weekly, Interval = 1, StartsAt = StartsAt,
                OrganizerUserId = SeriesOrganizer, LeadTimeDays = 14, ChainAsFollowUp = true, IsActive = true,
                LastProcessInstanceId = MeetingSeriesSchedule.ProcessInstanceId(series.Id, StartsAt),
                LastGeneratedAt = StartsAt, CreatedBy = "test"
            },
            staleVersion,
            CancellationToken.None);

        Assert.False(staleClaimAttempt);
    }

    [Fact]
    public async Task KS5_nothing_is_generated_before_the_lead_time_window_opens()
    {
        var series = NewSeries();
        var (handler, _, mediator) = Build(series);

        var result = await handler.Handle(
            new GenerateDueMeetingSeriesCommand(StartsAt.AddDays(-30), MaxSeries: 10, CorrelationId), CancellationToken.None);

        Assert.Equal(0, result.Data!.MeetingsGenerated);
        Assert.False(mediator.CreateMeetingWasSent);
    }

    [Fact]
    public async Task KS5_nothing_is_generated_once_the_series_has_ended()
    {
        var series = NewSeries(lastGeneratedAt: StartsAt, lastGeneratedMeetingId: Guid.NewGuid(), endsAt: StartsAt.AddDays(3));
        var (handler, _, mediator) = Build(series);

        var result = await handler.Handle(
            new GenerateDueMeetingSeriesCommand(StartsAt.AddDays(1), MaxSeries: 10, CorrelationId), CancellationToken.None);

        Assert.Equal(0, result.Data!.MeetingsGenerated);
        Assert.False(mediator.ScheduleFollowUpWasSent);
    }

    [Fact]
    public async Task KS5_an_inactive_series_generates_nothing()
    {
        var series = NewSeries(isActive: false);
        var (handler, _, mediator) = Build(series);

        var result = await handler.Handle(
            new GenerateDueMeetingSeriesCommand(StartsAt, MaxSeries: 10, CorrelationId), CancellationToken.None);

        Assert.Equal(0, result.Data!.SeriesConsidered);
        Assert.False(mediator.CreateMeetingWasSent);
    }

    [Fact]
    public async Task One_series_failure_does_not_abort_the_others_in_the_same_tenant()
    {
        var failing = NewSeries();
        var succeeding = NewSeries(name: "Aylık Yönetim Gözden Geçirmesi");
        var (handler, _, mediator) = Build(failing, succeeding);
        mediator.CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(Guid.NewGuid()), 201, CorrelationId);
        mediator.FailCreateForSeriesNamed = failing.Name;

        var result = await handler.Handle(
            new GenerateDueMeetingSeriesCommand(StartsAt, MaxSeries: 10, CorrelationId), CancellationToken.None);

        Assert.Equal(1, result.Data!.Failed);
        Assert.Equal(1, result.Data.MeetingsGenerated);
    }

    private static MeetingDto MakeMeetingDto(Guid id) => new(
        Id: id, Title: "t", MeetingTypeId: TypeId, MeetingTypeName: "Tür",
        StartAt: StartsAt, EndAt: StartsAt.AddHours(1), Location: null,
        OrganizerUserId: SeriesOrganizer, Description: null, FollowUpOfMeetingId: null,
        Lifecycle: MeetingLifecycle.Scheduled, CancellationReason: null, Version: 1,
        Attendees: [], AgendaItems: []);

    /// <summary>Answers ONLY CreateMeetingCommand/ScheduleFollowUpMeetingCommand — isolates this handler's own
    /// dispatch logic (KS1) from the real handlers those commands would otherwise reach.</summary>
    private sealed class RecordingMediator : IMediator
    {
        public Response<MeetingDto>? CreateMeetingResult { get; set; }
        public Response<ScheduleFollowUpMeetingResultDto>? ScheduleFollowUpResult { get; set; }
        public CreateMeetingRequest? LastCreateMeetingRequest { get; private set; }
        public Guid? LastFollowUpSourceId { get; private set; }
        public bool CreateMeetingWasSent { get; private set; }
        public bool ScheduleFollowUpWasSent { get; private set; }
        public int CreateMeetingCallCount { get; private set; }
        public string? FailCreateForSeriesNamed { get; set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is CreateMeetingCommand createMeeting)
            {
                if (FailCreateForSeriesNamed is not null && createMeeting.Request.Title == FailCreateForSeriesNamed)
                {
                    throw new InvalidOperationException("simulated series failure");
                }

                CreateMeetingWasSent = true;
                CreateMeetingCallCount++;
                LastCreateMeetingRequest = createMeeting.Request;
                return (Task<TResponse>)(object)Task.FromResult(CreateMeetingResult!);
            }

            if (request is ScheduleFollowUpMeetingCommand scheduleFollowUp)
            {
                ScheduleFollowUpWasSent = true;
                LastFollowUpSourceId = scheduleFollowUp.SourceMeetingId;
                return (Task<TResponse>)(object)Task.FromResult(ScheduleFollowUpResult!);
            }

            throw new NotSupportedException($"RecordingMediator does not support {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
