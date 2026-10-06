using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>MOD-0280-FU01 D10 — retire, never delete: entries that name the category keep resolving its label, and it
/// can come back.</summary>
public sealed class DeactivateWorkCategoryHandler : IRequestHandler<DeactivateWorkCategoryCommand, Response<WorkCategoryDto>>
{
    private readonly IWorkCategoryRepository _categories;
    private readonly ICurrentUserContext _currentUser;

    public DeactivateWorkCategoryHandler(IWorkCategoryRepository categories, ICurrentUserContext currentUser)
    {
        _categories = categories;
        _currentUser = currentUser;
    }

    public Task<Response<WorkCategoryDto>> Handle(DeactivateWorkCategoryCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WorkCategoryMapping.SetActiveAsync(
            _categories, request.Id, request.Request.ExpectedVersion, active: false, _currentUser.UserId, request.CorrelationId, ct);
    }
}
