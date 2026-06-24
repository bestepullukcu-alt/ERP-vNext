using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class SuspendTenantCommandHandler : IRequestHandler<SuspendTenantCommand, Response<TenantLifecycleResultDto>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IEventBus _eventBus;

    public SuspendTenantCommandHandler(
        ITenantRegistryRepository repository,
        ICurrentUserContext currentUser,
        IEventBus eventBus)
    {
        _repository = repository;
        _currentUser = currentUser;
        _eventBus = eventBus;
    }

    public async Task<Response<TenantLifecycleResultDto>> Handle(SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Response<TenantLifecycleResultDto>.Fail("Tenant not found.", 404);
        }

        if (SystemTenantRules.IsSystemTenant(tenant))
        {
            return Response<TenantLifecycleResultDto>.Fail("The platform system tenant cannot be suspended or deleted.", 400);
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            return Response<TenantLifecycleResultDto>.Fail("Tenant is already suspended.", 400);
        }

        if (tenant.Status == TenantStatus.Deactivated)
        {
            return Response<TenantLifecycleResultDto>.Fail("Deactivated tenant cannot be suspended.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.ActorName;
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Tenant suspended."
            : request.Reason.Trim();

        tenant.Status = TenantStatus.Suspended;
        tenant.SuspendedAt = now;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actor;
        tenant.ActivityTimeline.Add(new TenantActivityEvent
        {
            EventType = "tenant.suspended",
            Message = string.IsNullOrWhiteSpace(request.Reason) ? "Tenant suspended." : $"Tenant suspended. Reason: {reason}",
            At = now,
            Actor = actor
        });

        await _repository.UpdateAsync(tenant, cancellationToken);
        await _eventBus.PublishAsync(
            new TenantSuspendedV1(
                tenant.Id,
                now,
                reason,
                _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId),
            new EventPublishOptions
            {
                TenantId = tenant.Id,
                Producer = "Diten.Platform",
                OccurredAtUtc = now
            },
            cancellationToken);

        return Response<TenantLifecycleResultDto>.Success(new TenantLifecycleResultDto(tenant.Id, tenant.Status.ToString(), now, "Tenant suspended."));
    }
}
