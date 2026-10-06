using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

public sealed class ActivateWorkCategoryHandler : IRequestHandler<ActivateWorkCategoryCommand, Response<WorkCategoryDto>>
{
    private readonly IWorkCategoryRepository _categories;
    private readonly ICurrentUserContext _currentUser;

    public ActivateWorkCategoryHandler(IWorkCategoryRepository categories, ICurrentUserContext currentUser)
    {
        _categories = categories;
        _currentUser = currentUser;
    }

    public Task<Response<WorkCategoryDto>> Handle(ActivateWorkCategoryCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WorkCategoryMapping.SetActiveAsync(
            _categories, request.Id, request.Request.ExpectedVersion, active: true, _currentUser.UserId, request.CorrelationId, ct);
    }
}
