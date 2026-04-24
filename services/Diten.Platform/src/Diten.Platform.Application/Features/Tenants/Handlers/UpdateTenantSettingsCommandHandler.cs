using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class UpdateTenantSettingsCommandHandler : IRequestHandler<UpdateTenantSettingsCommand, TenantSettingsDto?>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateTenantSettingsCommandHandler(ITenantRegistryRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<TenantSettingsDto?> Handle(UpdateTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId.ToString()
            : "system";

        tenant.Settings.Language = request.Request.Language.Trim();
        tenant.Settings.Timezone = request.Request.Timezone.Trim();
        tenant.Settings.Currency = request.Request.Currency.Trim().ToUpperInvariant();
        tenant.Settings.Environment = request.Request.Environment.Trim();
        tenant.Environment = tenant.Settings.Environment;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actor;
        tenant.ActivityTimeline.Add(new Domain.Entities.TenantActivityEvent
        {
            EventType = "tenant.settings.updated",
            Message = "Tenant settings updated.",
            At = now,
            Actor = actor
        });

        await _repository.UpdateAsync(tenant, cancellationToken);

        return new TenantSettingsDto(
            tenant.Id,
            tenant.Region ?? "US",
            tenant.Settings.Language,
            tenant.Settings.Timezone,
            tenant.Settings.Currency,
            tenant.Settings.Environment);
    }
}
