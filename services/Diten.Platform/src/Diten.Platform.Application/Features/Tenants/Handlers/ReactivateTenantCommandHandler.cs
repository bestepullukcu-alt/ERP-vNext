using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class ReactivateTenantCommandHandler : IRequestHandler<ReactivateTenantCommand, TenantLifecycleResultDto?>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public ReactivateTenantCommandHandler(ITenantRegistryRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<TenantLifecycleResultDto?> Handle(ReactivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        if (tenant.Status == TenantStatus.Active)
        {
            throw new InvalidOperationException("Tenant is already active.");
        }

        if (tenant.Status == TenantStatus.Deactivated)
        {
            throw new InvalidOperationException("Deactivated tenant cannot be reactivated in MVP.");
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId.ToString()
            : "system";

        tenant.Status = TenantStatus.Active;
        tenant.ProvisioningStatus = "Completed";
        tenant.ActivatedAt = now;
        tenant.ProvisionedAt = now;
        tenant.SuspendedAt = null;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actor;

        var bootstrapStep = tenant.ProvisioningSteps.FirstOrDefault(x => x.Key == "bootstrap-platform");
        if (bootstrapStep != null)
        {
            bootstrapStep.Status = "Completed";
            bootstrapStep.CompletedAt = now;
            bootstrapStep.Detail = "Platform bootstrap completed.";
        }

        tenant.ActivityTimeline.Add(new TenantActivityEvent
        {
            EventType = "tenant.reactivated",
            Message = string.IsNullOrWhiteSpace(request.Reason) ? "Tenant reactivated." : $"Tenant reactivated. Note: {request.Reason.Trim()}",
            At = now,
            Actor = actor
        });

        await _repository.UpdateAsync(tenant, cancellationToken);

        return new TenantLifecycleResultDto(tenant.Id, tenant.Status.ToString(), now, "Tenant reactivated.");
    }
}
