using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>MOD-0280-FU01 D10 / §12 — edit a category. The code never changes: a different code in the request is
/// refused (<c>WORK_CATEGORY_CODE_IMMUTABLE</c>) rather than silently ignored.</summary>
public sealed class UpdateWorkCategoryHandler : IRequestHandler<UpdateWorkCategoryCommand, Response<WorkCategoryDto>>
{
    private readonly IWorkCategoryRepository _categories;
    private readonly ICurrentUserContext _currentUser;

    public UpdateWorkCategoryHandler(IWorkCategoryRepository categories, ICurrentUserContext currentUser)
    {
        _categories = categories;
        _currentUser = currentUser;
    }

    public async Task<Response<WorkCategoryDto>> Handle(UpdateWorkCategoryCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = await _categories.GetByIdAsync(request.Id, ct);
        if (category is null)
        {
            return Response<WorkCategoryDto>.Fail("Category not found.", 404, TimeEntryReasonCodes.CategoryNotFound, request.CorrelationId);
        }

        if (request.Request.Code is { } code && !string.Equals(code.Trim(), category.Code, StringComparison.Ordinal))
        {
            return Response<WorkCategoryDto>.Fail(
                "A category's code cannot change.", 400, TimeEntryReasonCodes.CategoryCodeImmutable, request.CorrelationId);
        }

        category.LabelText = request.Request.LabelText!.Trim();
        category.Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();
        category.CountsAsWork = request.Request.CountsAsWork;
        category.SortOrder = request.Request.SortOrder;
        category.UpdatedBy = _currentUser.UserId.ToString();

        return await _categories.UpdateAsync(category, request.Request.ExpectedVersion, ct)
            ? Response<WorkCategoryDto>.Success(WorkCategoryMapping.ToDto(category), correlationId: request.CorrelationId)
            : Response<WorkCategoryDto>.Fail(
                "The category changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.CategoryConcurrencyConflict,
                request.CorrelationId);
    }
}
