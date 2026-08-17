using AutoMapper;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.QueryHandlers;

public sealed class GetSubscriptionPlanByIdQueryHandler : IRequestHandler<GetSubscriptionPlanByIdQuery, Response<SubscriptionPlanDto>>
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly IMapper _mapper;

    public GetSubscriptionPlanByIdQueryHandler(ISubscriptionPlanRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Response<SubscriptionPlanDto>> Handle(GetSubscriptionPlanByIdQuery request, CancellationToken ct)
    {
        var plan = await _repository.GetByIdAsync(request.Id, ct);
        return plan is null
            ? Response<SubscriptionPlanDto>.Fail("Subscription plan not found.", 404)
            : Response<SubscriptionPlanDto>.Success(_mapper.Map<SubscriptionPlanDto>(plan));
    }
}
