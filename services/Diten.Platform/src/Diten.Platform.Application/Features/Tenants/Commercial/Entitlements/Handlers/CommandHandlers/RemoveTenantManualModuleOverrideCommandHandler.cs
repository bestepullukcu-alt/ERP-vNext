using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class RemoveTenantManualModuleOverrideCommandHandler : IRequestHandler<RemoveTenantManualModuleOverrideCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly IQuotaService _quotaService;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IEntitlementStateVersionRepository _versions;
    private readonly ITransactionalIntegrationEventWriter _events;
    private readonly ITransactionalAuditOutboxWriter _audit;
    private readonly ICurrentUserContext _currentUser;

    public RemoveTenantManualModuleOverrideCommandHandler(
        ITenantModuleEntitlementRepository repository,
        IModuleCatalogRepository moduleRepository,
        IQuotaService quotaService,
        IPlatformTransactionExecutor transactions,
        IEntitlementStateVersionRepository versions,
        ITransactionalIntegrationEventWriter events,
        ITransactionalAuditOutboxWriter audit,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _moduleRepository = moduleRepository;
        _quotaService = quotaService;
        _transactions = transactions;
        _versions = versions;
        _events = events;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(RemoveTenantManualModuleOverrideCommand request, CancellationToken ct)
    {
        var entitlement = await _repository.GetByIdAsync(request.TenantId, request.EntitlementId, ct);
        if (entitlement is null)
        {
            return TenantModuleEntitlementCommandSupport.NotFound();
        }

        // BL-500 — the list's own rule: only a manual override row is removed, and never one on a core or baseline
        // module (a stray baseline override must not read as "removing access" to it). Keys off the catalogue record,
        // not a code list.
        var refusal = await TenantModuleEntitlementActionGate.RefuseStoredAsync(
            _moduleRepository, entitlement, TenantModuleEntitlementRowActions.RemoveOverride, ct);
        if (refusal is not null)
        {
            return refusal;
        }

        try
        {
            var auditIntentId = Guid.NewGuid();
            await _transactions.ExecuteAsync(async (session, transactionCt) =>
            {
                await _repository.SoftDeleteAsync(session, request.TenantId, request.EntitlementId, request.Request.RowVersion, transactionCt);
                if (entitlement.IsEnabled)
                {
                    var release = await _quotaService.ReleaseEntitlementAsync(session, new ReleaseQuotaRequest(
                    request.TenantId,
                    QuotaKeys.ModulesMax,
                    1,
                    "ModuleEntitlement",
                    $"module-entitlement-remove:{request.EntitlementId}",
                    entitlement.ModuleCode,
                    "Tenant manual module override removed.",
                    null,
                    Guid.NewGuid().ToString()), transactionCt);

                    if (!release.IsSuccessful)
                    {
                        throw new PhysicalEntitlementMutationRejectedException(release.Errors, release.StatusCode);
                    }
                }

                await _versions.IncrementPhysicalEntitlementVersionAsync(session, request.TenantId, entitlement.ModuleCode, transactionCt);
                var eventId = Guid.NewGuid();
                var correlationId = Guid.NewGuid();
                var occurredAtUtc = DateTimeOffset.UtcNow;
                var actorId = _currentUser.UserId == Guid.Empty ? null : (Guid?)_currentUser.UserId;

                await _events.EnqueueAsync(
                session,
                new TenantEntitlementOverrideRemovedV1(
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
                transactionCt);
                await PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session, request.TenantId, correlationId,
                    auditIntentId, nameof(RemoveTenantManualModuleOverrideCommand), AuditOperation.Revoke,
                    entitlement.Id, entitlement.ModuleCode, transactionCt,
                    PhysicalEntitlementAuditIntent.StateOf(entitlement), PhysicalEntitlementAuditIntent.Removed());
                return true;
            }, ct);

            return Response<NoContent>.Success(204);
        }
        catch (PhysicalEntitlementMutationRejectedException exception)
        {
            return Response<NoContent>.Fail(exception.Errors, exception.StatusCode);
        }
        catch (TenantModuleEntitlementTenantMismatchException)
        {
            return TenantModuleEntitlementCommandSupport.NotFound();
        }
        catch (TenantModuleEntitlementConcurrencyException)
        {
            return TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
        }
    }
}
