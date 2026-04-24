using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class SuspendTenantCommandHandler : IRequestHandler<SuspendTenantCommand, TenantLifecycleResultDto?>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public SuspendTenantCommandHandler(ITenantRegistryRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<TenantLifecycleResultDto?> Handle(SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            throw new InvalidOperationException("Tenant is already suspended.");
        }

        if (tenant.Status == TenantStatus.Deactivated)
        {
            throw new InvalidOperationException("Deactivated tenant cannot be suspended.");
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId.ToString()
            : "system";

        tenant.Status = TenantStatus.Suspended;
        tenant.SuspendedAt = now;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actor;
        tenant.ActivityTimeline.Add(new TenantActivityEvent
        {
            EventType = "tenant.suspended",
            Message = string.IsNullOrWhiteSpace(request.Reason) ? "Tenant suspended." : $"Tenant suspended. Reason: {request.Reason.Trim()}",
            At = now,
            Actor = actor
        });

        await _repository.UpdateAsync(tenant, cancellationToken);

        return new TenantLifecycleResultDto(tenant.Id, tenant.Status.ToString(), now, "Tenant suspended.");
    }
}
