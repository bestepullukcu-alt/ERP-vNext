using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

public sealed class GetWorkCategoryListHandler : IRequestHandler<GetWorkCategoryListQuery, Response<IReadOnlyList<WorkCategoryDto>>>
{
    private readonly IWorkCategoryRepository _categories;

    public GetWorkCategoryListHandler(IWorkCategoryRepository categories) => _categories = categories;

    public async Task<Response<IReadOnlyList<WorkCategoryDto>>> Handle(GetWorkCategoryListQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var categories = await _categories.ListAsync(ct);
        IReadOnlyList<WorkCategoryDto> items = categories
            .Where(c => !request.ActiveOnly || c.IsActive)
            .Select(WorkCategoryMapping.ToDto)
            .ToList();
        return Response<IReadOnlyList<WorkCategoryDto>>.Success(items, correlationId: request.CorrelationId);
    }
}
