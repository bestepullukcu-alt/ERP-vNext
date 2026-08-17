using AutoMapper;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.QueryHandlers;

public sealed class GetSubscriptionPlanSummaryQueryHandler : IRequestHandler<GetSubscriptionPlanSummaryQuery, Response<SubscriptionPlanSummaryDto>>
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly IMapper _mapper;

    public GetSubscriptionPlanSummaryQueryHandler(ISubscriptionPlanRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Response<SubscriptionPlanSummaryDto>> Handle(GetSubscriptionPlanSummaryQuery request, CancellationToken ct)
    {
        var summary = await _repository.GetSummaryAsync(ct);
        return Response<SubscriptionPlanSummaryDto>.Success(_mapper.Map<SubscriptionPlanSummaryDto>(summary));
    }
}
