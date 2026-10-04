using Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Meetings.Queries;
using Diten.Platform.Domain.Entities.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

// MOD-0357 S3 — the Create/Edit form's two pickers.

public sealed class MeetingLookupHandlerTests
{
    private static readonly Guid Tenant = Diten.Platform.Application.Tests.Tasks.TaskTestData.Tenant;

    // BL-531 — the attendee picker is the task approver picker's own SEARCH (BL-512), not a second copy of it: the
    // meeting query only forwards, so the rule, the fold and the limits are the ones GetTaskDecisionMakerLookupHandler
    // already proves. Measured here through the real handler chain over the eligibility double.
    [Fact]
    public async Task GetMeetingAttendeeLookupHandler_searches_through_the_SAME_seam_the_task_approver_picker_uses()
    {
        var eligible = Guid.NewGuid();
        var mediator = new DecisionChainMediator();
        mediator.Eligibility.EligibleUserIds.Add(eligible);

        var handler = new GetMeetingAttendeeLookupHandler(mediator);
        var response = await handler.Handle(new GetMeetingAttendeeLookupQuery("corr", "Position", null), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Contains(response.Data!.People, p => p.UserId == eligible);
        var forwarded = Assert.IsType<Diten.Platform.Application.Features.Tasks.GetTaskDecisionMakerLookupQuery>(Assert.Single(mediator.Forwarded));
        Assert.Equal("Position", forwarded.Search);
    }

    [Fact]
    public async Task GetMeetingAttendeeLookupHandler_without_a_search_is_400_and_never_the_whole_list()
    {
        var mediator = new DecisionChainMediator();
        mediator.Eligibility.EligibleUserIds.Add(Guid.NewGuid());

        var response = await new GetMeetingAttendeeLookupHandler(mediator)
            .Handle(new GetMeetingAttendeeLookupQuery("corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(Diten.Platform.Application.Features.Tasks.DecisionMakerLookup.ReasonCodes.SearchTooShort, response.ReasonCode);
        Assert.Null(response.Data);
    }

    /// <summary>Runs the REAL BL-512 search handler over <see cref="FakeEligibilityMediator"/>.</summary>
    private sealed class DecisionChainMediator : MediatR.IMediator
    {
        public FakeEligibilityMediator Eligibility { get; } = new();
        public List<object> Forwarded { get; } = [];

        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken ct = default)
        {
            Forwarded.Add(request);
            if (request is Diten.Platform.Application.Features.Tasks.GetTaskDecisionMakerLookupQuery query)
            {
                return (Task<TResponse>)(object)new Diten.Platform.Application.Features.Tasks.GetTaskDecisionMakerLookupHandler(Eligibility).Handle(query, ct);
            }

            throw new NotSupportedException(request.GetType().Name);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : MediatR.IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : MediatR.INotification => throw new NotSupportedException();
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
