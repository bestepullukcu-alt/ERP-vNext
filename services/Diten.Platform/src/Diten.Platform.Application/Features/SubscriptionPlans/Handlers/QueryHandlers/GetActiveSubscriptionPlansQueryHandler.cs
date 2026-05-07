using AutoMapper;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.QueryHandlers;

public sealed class GetActiveSubscriptionPlansQueryHandler
    : IRequestHandler<GetActiveSubscriptionPlansQuery, Response<IReadOnlyList<SubscriptionPlanListItemDto>>>
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly IMapper _mapper;

    public GetActiveSubscriptionPlansQueryHandler(ISubscriptionPlanRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Response<IReadOnlyList<SubscriptionPlanListItemDto>>> Handle(GetActiveSubscriptionPlansQuery request, CancellationToken ct)
    {
        var items = await _repository.GetActiveAsync(ct);
        return Response<IReadOnlyList<SubscriptionPlanListItemDto>>.Success(_mapper.Map<IReadOnlyList<SubscriptionPlanListItemDto>>(items));
    }
}
