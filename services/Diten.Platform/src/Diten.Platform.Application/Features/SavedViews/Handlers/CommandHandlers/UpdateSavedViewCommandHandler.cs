using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.SavedViews.Commands;
using Diten.Platform.Application.Models;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SavedViews.Handlers.CommandHandlers;

public sealed class UpdateSavedViewCommandHandler : IRequestHandler<UpdateSavedViewCommand, SavedViewModel?>
{
    private readonly ISavedViewRepository _repository;
    private readonly ICurrentUserContext _currentUserContext;

    public UpdateSavedViewCommandHandler(
        ISavedViewRepository repository,
        ICurrentUserContext currentUserContext)
    {
        _repository = repository;
        _currentUserContext = currentUserContext;
    }

    public async Task<SavedViewModel?> Handle(UpdateSavedViewCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUserContext.IsAuthenticated || _currentUserContext.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Authenticated user context is required.");
        }

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null || entity.UserId != _currentUserContext.UserId)
        {
            return null;
        }

        entity.ModuleKey = NormalizeString(request.ModuleKey, entity.ModuleKey);
        entity.PageKey = NormalizeString(request.PageKey, entity.PageKey);
        entity.ViewName = NormalizeString(request.ViewName, entity.ViewName);
        entity.ViewDefinitionJson = request.ViewDefinitionJson ?? entity.ViewDefinitionJson;
        entity.Visibility = NormalizeVisibility(request.Visibility, entity.Visibility);

        if (request.IsDefault.HasValue)
        {
            entity.IsDefault = request.IsDefault.Value;
        }

        entity.ModifiedBy = _currentUserContext.UserId.ToString();

        if (entity.IsDefault)
        {
            await _repository.ClearDefaultsAsync(entity.UserId, entity.ModuleKey, entity.PageKey, entity.Id, cancellationToken);
        }

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return SavedViewModel.FromEntity(updated);
    }

    private static string NormalizeString(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeVisibility(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }
}
