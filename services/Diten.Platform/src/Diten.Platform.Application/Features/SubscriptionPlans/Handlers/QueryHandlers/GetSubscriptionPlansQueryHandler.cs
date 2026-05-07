using AutoMapper;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Queries;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.QueryHandlers;

public sealed class GetSubscriptionPlansQueryHandler
    : IRequestHandler<GetSubscriptionPlansQuery, Response<PagedResult<SubscriptionPlanListItemDto>>>
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly IMapper _mapper;

    public GetSubscriptionPlansQueryHandler(ISubscriptionPlanRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Response<PagedResult<SubscriptionPlanListItemDto>>> Handle(GetSubscriptionPlansQuery request, CancellationToken ct)
    {
        var query = new SubscriptionPlansQuery(
            request.Filter.Search,
            request.Filter.IsActive,
            request.Filter.IsTrialPlan,
            request.Filter.Page,
            request.Filter.PageSize,
            request.Filter.Sort);

        var (items, totalCount) = await _repository.QueryAsync(query, ct);
        var normalizedPageSize = Math.Clamp(request.Filter.PageSize, 1, 200);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var payload = new PagedResult<SubscriptionPlanListItemDto>(
            _mapper.Map<IReadOnlyList<SubscriptionPlanListItemDto>>(items),
            Math.Max(request.Filter.Page, 1),
            normalizedPageSize,
            totalCount,
            totalPages);

        return Response<PagedResult<SubscriptionPlanListItemDto>>.Success(payload);
    }
}
