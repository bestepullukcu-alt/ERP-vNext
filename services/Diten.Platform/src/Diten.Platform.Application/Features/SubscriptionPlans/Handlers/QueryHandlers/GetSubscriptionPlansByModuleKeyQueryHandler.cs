using AutoMapper;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.QueryHandlers;

public sealed class GetSubscriptionPlansByModuleKeyQueryHandler
    : IRequestHandler<GetSubscriptionPlansByModuleKeyQuery, Response<IReadOnlyList<SubscriptionPlanListItemDto>>>
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly IMapper _mapper;

    public GetSubscriptionPlansByModuleKeyQueryHandler(ISubscriptionPlanRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Response<IReadOnlyList<SubscriptionPlanListItemDto>>> Handle(GetSubscriptionPlansByModuleKeyQuery request, CancellationToken ct)
    {
        var items = await _repository.GetByIncludedModuleKeyAsync(request.ModuleKey, ct);
        return Response<IReadOnlyList<SubscriptionPlanListItemDto>>.Success(_mapper.Map<IReadOnlyList<SubscriptionPlanListItemDto>>(items));
    }
}
