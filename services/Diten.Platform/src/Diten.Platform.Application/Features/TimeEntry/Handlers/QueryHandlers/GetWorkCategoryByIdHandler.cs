using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

public sealed class GetWorkCategoryByIdHandler : IRequestHandler<GetWorkCategoryByIdQuery, Response<WorkCategoryDto>>
{
    private readonly IWorkCategoryRepository _categories;

    public GetWorkCategoryByIdHandler(IWorkCategoryRepository categories) => _categories = categories;

    public async Task<Response<WorkCategoryDto>> Handle(GetWorkCategoryByIdQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var category = await _categories.GetByIdAsync(request.Id, ct);
        return category is null
            ? Response<WorkCategoryDto>.Fail("Category not found.", 404, TimeEntryReasonCodes.CategoryNotFound, request.CorrelationId)
            : Response<WorkCategoryDto>.Success(WorkCategoryMapping.ToDto(category), correlationId: request.CorrelationId);
    }
}
