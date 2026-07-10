using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class EnableTenantModuleEntitlementCommandHandler : IRequestHandler<EnableTenantModuleEntitlementCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IQuotaService _quotaService;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserContext _currentUser;

    public EnableTenantModuleEntitlementCommandHandler(
        ITenantModuleEntitlementRepository repository,
        IQuotaService quotaService,
        IEventBus eventBus,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _quotaService = quotaService;
        _eventBus = eventBus;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(EnableTenantModuleEntitlementCommand request, CancellationToken ct)
    {
        var entitlement = await _repository.GetByIdAsync(request.TenantId, request.EntitlementId, ct);
        if (entitlement is null)
        {
            return Response<NoContent>.Fail("Entitlement was not found.", 404);
        }

        try
        {
            var wasEnabled = entitlement.IsEnabled;
            if (!wasEnabled)
            {
                // FIX-QUOTA-DRIFT — reconcile modules.max CurrentValue to the REAL enabled-entitlement count
                // (RecalculateAsync → ComputeCurrentUsageAsync) BEFORE the atomic consume, so a historically drifted
                // counter self-heals and a legitimate enable is not blocked by a phantom +1. Best-effort: the consume
                // that follows is the authoritative limit check, so a genuine over-limit still returns 409.
                await _quotaService.RecalculateAsync(new RecalculateQuotaUsageRequest(
                    request.TenantId,
                    QuotaKeys.ModulesMax,
                    "ModuleEntitlement",
                    null,
                    entitlement.ModuleCode,
                    "Reconcile modules.max to the real enabled count before enforcing.",
                    null,
                    Guid.NewGuid().ToString()), ct);

                var consume = await _quotaService.TryConsumeAsync(new TryConsumeQuotaRequest(
                    request.TenantId,
                    QuotaKeys.ModulesMax,
                    1,
                    "ModuleEntitlement",
                    // FIX-ENTITLEMENT-REENABLE — the quota dedup key must scope to THIS enable event, not the
                    // entitlement's lifetime. Including the row's current RowVersion (UpdateAsync mints a fresh one on
                    // every transition) means a re-enable after a disable is a distinct operation that consumes cleanly,
                    // while a genuine retry of the SAME enable (row not yet advanced) reuses the key and is still deduped
                    // — so it never double-consumes. A deterministic lifetime key permanently blocked re-enable.
                    $"module-entitlement-enable:{request.EntitlementId}:{Convert.ToHexString(entitlement.RowVersion)}",
                    entitlement.ModuleCode,
                    "Tenant module entitlement enabled.",
                    null,
                    Guid.NewGuid().ToString()), ct);

                if (!consume.IsSuccessful)
                {
                    return Response<NoContent>.Fail(consume.Errors, consume.StatusCode);
                }
            }

            entitlement.IsEnabled = true;
            await _repository.UpdateAsync(entitlement, request.RowVersion, ct);
            if (!wasEnabled)
            {
                var eventId = Guid.NewGuid();
                var correlationId = Guid.NewGuid();
                var occurredAtUtc = DateTimeOffset.UtcNow;
                var actorId = _currentUser.UserId == Guid.Empty ? null : (Guid?)_currentUser.UserId;

                await _eventBus.PublishAsync(
                    new TenantEntitlementEnabledV1(
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
