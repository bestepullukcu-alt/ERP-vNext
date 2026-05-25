using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class UpdateTenantModuleEntitlementExpiryCommandHandler : IRequestHandler<UpdateTenantModuleEntitlementExpiryCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserContext _currentUser;

    public UpdateTenantModuleEntitlementExpiryCommandHandler(
        ITenantModuleEntitlementRepository repository,
        IEventBus eventBus,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _eventBus = eventBus;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(UpdateTenantModuleEntitlementExpiryCommand request, CancellationToken ct)
    {
        var entitlement = await _repository.GetByIdAsync(request.TenantId, request.EntitlementId, ct);
        if (entitlement is null)
        {
            return Response<NoContent>.Fail("Entitlement was not found.", 404);
        }

        try
        {
            var expiryChanged = entitlement.ExpiryDateUtc != request.Request.ExpiryDateUtc;
            entitlement.ExpiryDateUtc = request.Request.ExpiryDateUtc;
            if (!string.IsNullOrWhiteSpace(request.Request.Reason))
            {
                entitlement.Reason = request.Request.Reason;
            }

            await _repository.UpdateAsync(entitlement, request.Request.RowVersion, ct);
            if (expiryChanged)
            {
                var eventId = Guid.NewGuid();
                var correlationId = Guid.NewGuid();
                var occurredAtUtc = DateTimeOffset.UtcNow;
                var actorId = _currentUser.UserId == Guid.Empty ? null : (Guid?)_currentUser.UserId;

                await _eventBus.PublishAsync(
                    new TenantEntitlementExpiryUpdatedV1(
                        eventId,
                        occurredAtUtc,
                        request.TenantId,
                        correlationId,
                        actorId,
                        entitlement.ModuleCode),
                    new EventPublishOptions
                    {
                        EventId = eventId,
                        CorrelationId = correlationId,
                        TenantId = request.TenantId,
                        Producer = "Diten.Platform",
                        OccurredAtUtc = occurredAtUtc
                    },
                    ct);
            }

            return Response<NoContent>.Success(204);
        }
        catch (TenantModuleEntitlementConcurrencyException)
        {
            return TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
        }
    }
}
