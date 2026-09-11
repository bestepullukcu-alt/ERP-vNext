using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

public sealed class MeetingTypeCommandHandlerTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;

    [Fact]
    public async Task Create_with_a_name_that_already_exists_is_409_MEETING_TYPE_NAME_DUPLICATE()
    {
        var types = new FakeMeetingTypeRepository { Tenant = Tenant };
        types.Seed(new MeetingType { TenantId = Tenant, Name = "Yönetim Gözden Geçirme" });

        var handler = new CreateMeetingTypeHandler(types, new FakeTenantContext(Tenant), new FakeCurrentUserContext(Guid.NewGuid()));
        var response = await handler.Handle(
            new CreateMeetingTypeCommand(new CreateMeetingTypeRequest("Yönetim Gözden Geçirme", null, null, false, false, false), "corr"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.TypeNameDuplicate, response.ReasonCode);
    }

    [Fact]
    public async Task Delete_a_type_that_a_live_meeting_references_is_409_MEETING_TYPE_IN_USE()
    {
        var types = new FakeMeetingTypeRepository { Tenant = Tenant };
        var meetings = new FakeMeetingRepository { Tenant = Tenant };
        var type = new MeetingType { TenantId = Tenant, Name = "MGMT-REVIEW" };
        types.Seed(type);
        meetings.Seed(new Meeting
        {
            TenantId = Tenant, Title = "T", MeetingTypeId = type.Id,
            StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
            OrganizerUserId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid().ToString()
        });

        var handler = new DeleteMeetingTypeHandler(types, meetings);
        var response = await handler.Handle(new DeleteMeetingTypeCommand(type.Id, "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.TypeInUse, response.ReasonCode);
    }

    [Fact]
    public async Task Delete_an_UNUSED_type_succeeds()
    {
        var types = new FakeMeetingTypeRepository { Tenant = Tenant };
        var meetings = new FakeMeetingRepository { Tenant = Tenant };
        var type = new MeetingType { TenantId = Tenant, Name = "Serbest Tür" };
        types.Seed(type);

        var handler = new DeleteMeetingTypeHandler(types, meetings);
        var response = await handler.Handle(new DeleteMeetingTypeCommand(type.Id, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Null(await types.GetByIdAsync(type.Id));
    }

    [Fact]
    public async Task AgendaTemplate_beyond_the_line_limit_is_refused_by_the_validator_shape()
    {
        var request = new CreateMeetingTypeRequest(
            "Uzun Şablon", Enumerable.Repeat("madde", 21).ToList(), null, false, false, false);
        var validator = new Diten.Platform.Application.Features.Meetings.Validators.CreateMeetingTypeValidator();

        var result = await validator.ValidateAsync(new CreateMeetingTypeCommand(request, "corr"));

        Assert.False(result.IsValid);
    }
}
