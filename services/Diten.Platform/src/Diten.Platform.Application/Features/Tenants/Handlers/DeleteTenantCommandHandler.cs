using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class DeleteTenantCommandHandler : IRequestHandler<DeleteTenantCommand, Response<NoContent>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ITenantDomainRepository _domainRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IEventBus _eventBus;

    public DeleteTenantCommandHandler(
        ITenantRegistryRepository repository,
        ITenantDomainRepository domainRepository,
        ICurrentUserContext currentUser,
        IEventBus eventBus)
    {
        _repository = repository;
        _domainRepository = domainRepository;
        _currentUser = currentUser;
        _eventBus = eventBus;
    }

    public async Task<Response<NoContent>> Handle(DeleteTenantCommand request, CancellationToken ct)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, ct);
        if (tenant is null)
        {
            return Response<NoContent>.Fail("Tenant not found.", 404);
        }

        if (tenant.Status == TenantStatus.Active)
        {
            return Response<NoContent>.Fail("Active tenants must be suspended before deletion.", 400);
        }

        await _repository.DeleteAsync(tenant.Id, ct);
        var domains = await _domainRepository.GetByTenantIdAsync(tenant.Id, ct);
        foreach (var domain in domains)
        {
            await _domainRepository.DeleteAsync(domain.Id, ct);
        }

        var now = DateTimeOffset.UtcNow;
        await _eventBus.PublishAsync(
            new TenantCancelledV1(
                tenant.Id,
                now,
                now,
                null,
                _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId),
            new EventPublishOptions
            {
                TenantId = tenant.Id,
                Producer = "Diten.Platform",
                OccurredAtUtc = now
            },
            ct);

        return Response<NoContent>.Success(204);
    }
}
