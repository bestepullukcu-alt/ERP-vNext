using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Application.Features.SavedViews.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SavedViews.Handlers.CommandHandlers;

public sealed class DeleteSavedViewCommandHandler : IRequestHandler<DeleteSavedViewCommand, bool>
{
    private readonly ISavedViewRepository _repository;
    private readonly ICurrentUserContext _currentUserContext;

    public DeleteSavedViewCommandHandler(
        ISavedViewRepository repository,
        ICurrentUserContext currentUserContext)
    {
        _repository = repository;
        _currentUserContext = currentUserContext;
    }

    public async Task<bool> Handle(DeleteSavedViewCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUserContext.IsAuthenticated || _currentUserContext.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Authenticated user context is required.");
        }

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null || entity.UserId != _currentUserContext.UserId)
        {
            return false;
        }

        return await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}
