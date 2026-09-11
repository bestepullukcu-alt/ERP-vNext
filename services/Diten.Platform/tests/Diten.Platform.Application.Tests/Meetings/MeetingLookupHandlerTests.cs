using Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Meetings.Queries;
using Diten.Platform.Domain.Entities.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

// MOD-0357 S3 — the Create/Edit form's two pickers.

public sealed class MeetingLookupHandlerTests
{
    private static readonly Guid Tenant = Diten.Platform.Application.Tests.Tasks.TaskTestData.Tenant;

    [Fact]
    public async Task GetMeetingAttendeeLookupHandler_forwards_to_the_SAME_seam_MOD_0024s_own_picker_uses()
    {
        var eligible = Guid.NewGuid();
        var mediator = new FakeEligibilityMediator();
        mediator.EligibleUserIds.Add(eligible);

        var handler = new GetMeetingAttendeeLookupHandler(mediator);
        var response = await handler.Handle(new GetMeetingAttendeeLookupQuery("corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Contains(response.Data!.People, p => p.UserId == eligible);
    }

    [Fact]
    public async Task GetMeetingTypeLookupHandler_returns_id_and_name_only_ordered_by_name()
    {
        var types = new FakeMeetingTypeRepository { Tenant = Tenant };
        types.Seed(new MeetingType { TenantId = Tenant, Name = "Zeta" });
        types.Seed(new MeetingType { TenantId = Tenant, Name = "Alpha" });

        var handler = new GetMeetingTypeLookupHandler(types);
        var response = await handler.Handle(new GetMeetingTypeLookupQuery("corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(["Alpha", "Zeta"], response.Data!.Select(t => t.Name));
    }
}
