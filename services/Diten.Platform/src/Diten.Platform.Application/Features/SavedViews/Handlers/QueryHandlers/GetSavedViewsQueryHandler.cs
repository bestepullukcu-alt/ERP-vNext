using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.SavedViews.Queries;
using Diten.Platform.Application.Models;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SavedViews.Handlers.QueryHandlers;

public sealed class GetSavedViewsQueryHandler : IRequestHandler<GetSavedViewsQuery, IReadOnlyList<SavedViewModel>>
{
    private readonly ISavedViewRepository _repository;
    private readonly ICurrentUserContext _currentUserContext;

    public GetSavedViewsQueryHandler(
        ISavedViewRepository repository,
        ICurrentUserContext currentUserContext)
    {
        _repository = repository;
        _currentUserContext = currentUserContext;
    }

    public async Task<IReadOnlyList<SavedViewModel>> Handle(GetSavedViewsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUserContext.IsAuthenticated || _currentUserContext.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Authenticated user context is required.");
        }

        var entities = await _repository.GetListAsync(
            _currentUserContext.UserId,
            request.ModuleKey.Trim(),
            request.PageKey.Trim(),
            cancellationToken);

        return entities.Select(SavedViewModel.FromEntity).ToList();
    }
}
