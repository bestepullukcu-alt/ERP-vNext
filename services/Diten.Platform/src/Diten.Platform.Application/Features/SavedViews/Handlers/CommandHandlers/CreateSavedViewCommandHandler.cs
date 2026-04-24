using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Application.Features.SavedViews.Commands;
using Diten.Platform.Application.Models;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.SavedViews.Handlers.CommandHandlers;

public sealed class CreateSavedViewCommandHandler : IRequestHandler<CreateSavedViewCommand, SavedViewModel>
{
    private readonly ISavedViewRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUserContext;

    public CreateSavedViewCommandHandler(
        ISavedViewRepository repository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
    }

    public async Task<SavedViewModel> Handle(CreateSavedViewCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUserContext.IsAuthenticated || _currentUserContext.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Authenticated user context is required.");
        }

        var visibility = NormalizeVisibility(request.Visibility);
        var entity = new SavedView
        {
            TenantId = _tenantContext.TenantId,
            UserId = _currentUserContext.UserId,
            CreatedBy = _currentUserContext.UserId.ToString(),
            ModifiedBy = _currentUserContext.UserId.ToString(),
            ModuleKey = request.ModuleKey.Trim(),
            PageKey = request.PageKey.Trim(),
            ViewName = request.ViewName.Trim(),
            ViewDefinitionJson = request.ViewDefinitionJson,
            IsDefault = request.IsDefault,
            Visibility = visibility
        };

        if (entity.IsDefault)
        {
            await _repository.ClearDefaultsAsync(entity.UserId, entity.ModuleKey, entity.PageKey, null, cancellationToken);
        }

        var created = await _repository.InsertAsync(entity, cancellationToken);
        return SavedViewModel.FromEntity(created);
    }

    private static string NormalizeVisibility(string? visibility)
    {
        return string.IsNullOrWhiteSpace(visibility)
            ? "private"
            : visibility.Trim().ToLowerInvariant();
    }
}
