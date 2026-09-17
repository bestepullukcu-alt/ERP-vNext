using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>MOD-0357 S11 — the recurring-series rule's own CRUD, mirroring MeetingTypeCommandHandlerTests'
/// coverage shape for S8. KS3's own field-required rule is exercised here (create/update refuse Guid.Empty).</summary>
public sealed class MeetingSeriesCommandHandlerTests
{
    private const string CorrelationId = "corr-1";
    private static readonly DateTimeOffset StartsAt = new(2027, 2, 1, 9, 0, 0, TimeSpan.Zero);

    private static (CreateMeetingSeriesHandler Handler, FakeMeetingSeriesRepository Series, FakeMeetingTypeRepository Types)
        BuildCreateHandler(Guid? typeId = null)
    {
        var types = new FakeMeetingTypeRepository { Tenant = TaskTestData.Tenant };
        types.Seed(new MeetingType { Id = typeId ?? TypeId, TenantId = TaskTestData.Tenant, Name = "Tür", CreatedBy = "test" });
        var series = new FakeMeetingSeriesRepository { Tenant = TaskTestData.Tenant };
        var handler = new CreateMeetingSeriesHandler(series, types, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
        return (handler, series, types);
    }

    private static readonly Guid TypeId = Guid.Parse("33333333-0000-0000-0000-000000000003");

    private static CreateMeetingSeriesRequest ValidCreateRequest(
        string name = "Haftalık Kalite Toplantısı",
        Guid? organizerUserId = null,
        int interval = 1,
        int leadTimeDays = 14,
        DateTimeOffset? endsAt = null) => new(
        Name: name,
        MeetingTypeId: TypeId,
        Frequency: MeetingSeriesFrequency.Weekly,
        Interval: interval,
        StartsAt: StartsAt,
        EndsAt: endsAt,
        DurationMinutes: 60,
        Location: null,
        OrganizerUserId: organizerUserId ?? TaskTestData.Me,
        AttendeeUserIds: null,
        LeadTimeDays: leadTimeDays,
        ChainAsFollowUp: true,
        IsActive: true);

    [Fact]
    public async Task Creates_a_series_with_the_requested_fields()
    {
        var (handler, seriesRepo, _) = BuildCreateHandler();

        var result = await handler.Handle(new CreateMeetingSeriesCommand(ValidCreateRequest(), CorrelationId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Haftalık Kalite Toplantısı", result.Data!.Name);
        Assert.Equal("Tür", result.Data.MeetingTypeName);
        Assert.Single((await seriesRepo.ListAllAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task A_duplicate_name_is_409_not_a_second_row()
    {
        var (handler, seriesRepo, _) = BuildCreateHandler();
        await handler.Handle(new CreateMeetingSeriesCommand(ValidCreateRequest(), CorrelationId), CancellationToken.None);

        var second = await handler.Handle(new CreateMeetingSeriesCommand(ValidCreateRequest(), CorrelationId), CancellationToken.None);

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(MeetingReasonCodes.SeriesNameDuplicate, second.ReasonCode);
        Assert.Single(await seriesRepo.ListAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_unknown_meeting_type_is_refused()
    {
        var (handler, _, _) = BuildCreateHandler();
        var request = ValidCreateRequest() with { MeetingTypeId = Guid.NewGuid() };

        var result = await handler.Handle(new CreateMeetingSeriesCommand(request, CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.TypeNotFound, result.ReasonCode);
    }

    [Fact]
    public async Task KS3_an_empty_organizer_is_refused_not_silently_accepted()
    {
        var (handler, _, _) = BuildCreateHandler();
        var request = ValidCreateRequest(organizerUserId: Guid.Empty);

        var result = await handler.Handle(new CreateMeetingSeriesCommand(request, CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.SeriesOrganizerRequired, result.ReasonCode);
    }

    [Fact]
    public async Task An_end_at_or_before_the_start_is_refused()
    {
        var (handler, _, _) = BuildCreateHandler();
        var request = ValidCreateRequest(endsAt: StartsAt);

        var result = await handler.Handle(new CreateMeetingSeriesCommand(request, CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(MeetingReasonCodes.SeriesInvalidWindow, result.ReasonCode);
    }

    [Fact]
    public async Task An_interval_below_one_is_refused()
    {
        var (handler, _, _) = BuildCreateHandler();
        var request = ValidCreateRequest(interval: 0);

        var result = await handler.Handle(new CreateMeetingSeriesCommand(request, CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(MeetingReasonCodes.SeriesIntervalInvalid, result.ReasonCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public async Task A_lead_time_outside_1_to_90_is_refused(int leadTimeDays)
    {
        var (handler, _, _) = BuildCreateHandler();
        var request = ValidCreateRequest(leadTimeDays: leadTimeDays);

        var result = await handler.Handle(new CreateMeetingSeriesCommand(request, CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(MeetingReasonCodes.SeriesInvalidWindow, result.ReasonCode);
    }

    [Fact]
    public async Task Update_changes_fields_and_can_deactivate_without_touching_anything_else()
    {
        var (createHandler, seriesRepo, types) = BuildCreateHandler();
        var created = await createHandler.Handle(new CreateMeetingSeriesCommand(ValidCreateRequest(), CorrelationId), CancellationToken.None);
        var updateHandler = new UpdateMeetingSeriesHandler(seriesRepo, types);

        var updateRequest = new UpdateMeetingSeriesRequest(
            Name: "Haftalık Kalite Toplantısı", MeetingTypeId: TypeId, Frequency: MeetingSeriesFrequency.Weekly,
            Interval: 1, StartsAt: StartsAt, EndsAt: null, DurationMinutes: 90, Location: "Oda 2",
            OrganizerUserId: TaskTestData.Me, AttendeeUserIds: null, LeadTimeDays: 14, ChainAsFollowUp: true,
            IsActive: false, ExpectedVersion: created.Data!.Version);

        var result = await updateHandler.Handle(
            new UpdateMeetingSeriesCommand(created.Data!.Id, updateRequest, CorrelationId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var stored = await seriesRepo.GetByIdAsync(created.Data.Id, CancellationToken.None);
        Assert.False(stored!.IsActive);
        Assert.Equal(90, stored.DurationMinutes);
        Assert.Equal("Oda 2", stored.Location);
    }

    [Fact]
    public async Task Update_with_a_stale_version_is_a_409_concurrency_conflict()
    {
        var (createHandler, seriesRepo, types) = BuildCreateHandler();
        var created = await createHandler.Handle(new CreateMeetingSeriesCommand(ValidCreateRequest(), CorrelationId), CancellationToken.None);
        var updateHandler = new UpdateMeetingSeriesHandler(seriesRepo, types);

        var updateRequest = new UpdateMeetingSeriesRequest(
            Name: "Haftalık Kalite Toplantısı", MeetingTypeId: TypeId, Frequency: MeetingSeriesFrequency.Weekly,
            Interval: 1, StartsAt: StartsAt, EndsAt: null, DurationMinutes: 60, Location: null,
            OrganizerUserId: TaskTestData.Me, AttendeeUserIds: null, LeadTimeDays: 14, ChainAsFollowUp: true,
            IsActive: true, ExpectedVersion: created.Data!.Version + 1);

        var result = await updateHandler.Handle(
            new UpdateMeetingSeriesCommand(created.Data!.Id, updateRequest, CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.ConcurrencyConflict, result.ReasonCode);
    }

    [Fact]
    public async Task Delete_removes_only_the_rule_row()
    {
        var (createHandler, seriesRepo, types) = BuildCreateHandler();
        var created = await createHandler.Handle(new CreateMeetingSeriesCommand(ValidCreateRequest(), CorrelationId), CancellationToken.None);
        var deleteHandler = new DeleteMeetingSeriesHandler(seriesRepo);

        var result = await deleteHandler.Handle(new DeleteMeetingSeriesCommand(created.Data!.Id, CorrelationId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Null(await seriesRepo.GetByIdAsync(created.Data.Id, CancellationToken.None));
        _ = types;
    }

    [Fact]
    public async Task Deleting_an_unknown_series_is_404()
    {
        var (_, seriesRepo, _) = BuildCreateHandler();
        var deleteHandler = new DeleteMeetingSeriesHandler(seriesRepo);

        var result = await deleteHandler.Handle(new DeleteMeetingSeriesCommand(Guid.NewGuid(), CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.SeriesNotFound, result.ReasonCode);
    }
}
