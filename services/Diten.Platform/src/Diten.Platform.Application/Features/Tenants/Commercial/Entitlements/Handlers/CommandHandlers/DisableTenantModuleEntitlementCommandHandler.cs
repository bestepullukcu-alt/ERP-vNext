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

public sealed class DisableTenantModuleEntitlementCommandHandler : IRequestHandler<DisableTenantModuleEntitlementCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly ITenantSubscriptionRepository _subscriptions;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly IQuotaService _quotaService;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IEntitlementStateVersionRepository _versions;
    private readonly ITransactionalIntegrationEventWriter _events;
    private readonly ITransactionalAuditOutboxWriter _audit;
    private readonly ICurrentUserContext _currentUser;

    public DisableTenantModuleEntitlementCommandHandler(
        ITenantModuleEntitlementRepository repository,
        IModuleCatalogRepository moduleRepository,
        ITenantSubscriptionRepository subscriptions,
        ISubscriptionPlanRepository plans,
        IQuotaService quotaService,
        IPlatformTransactionExecutor transactions,
        IEntitlementStateVersionRepository versions,
        ITransactionalIntegrationEventWriter events,
        ITransactionalAuditOutboxWriter audit,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _moduleRepository = moduleRepository;
        _subscriptions = subscriptions;
        _plans = plans;
        _quotaService = quotaService;
        _transactions = transactions;
        _versions = versions;
        _events = events;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(DisableTenantModuleEntitlementCommand request, CancellationToken ct)
    {
        var requestedCode = TenantModuleEntitlementCommandSupport.NormalizeModuleCode(request.Request.ModuleCode!);

        try
        {
            if (request.Request.PhysicalEntitlementId.HasValue)
            {
                return await DisableStoredRowAsync(request, requestedCode, request.Request.PhysicalEntitlementId.Value, ct);
            }

            return await SuspendPlanLineAsync(request, requestedCode, ct);
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

    /// <summary>
    /// A stored row, named by its id. The module is the ROW's: the code in the body only has to agree with it — a body
    /// naming another module is refused, never used (it once chose the core/baseline check and the quota release).
    /// </summary>
    private async Task<Response<NoContent>> DisableStoredRowAsync(
        DisableTenantModuleEntitlementCommand request, string requestedCode, Guid entitlementId, CancellationToken ct)
    {
        var entitlement = await _repository.GetByIdAsync(request.TenantId, entitlementId, ct);
        if (entitlement is null)
        {
            return TenantModuleEntitlementCommandSupport.NotFound();
        }

        if (!string.Equals(entitlement.ModuleCode, requestedCode, StringComparison.Ordinal))
        {
            return Response<NoContent>.Fail("The entitlement row belongs to another module.", 400, TenantModuleEntitlementRefusalCodes.ModuleMismatch);
        }

        var refusal = await TenantModuleEntitlementActionGate.RefuseStoredAsync(
            _moduleRepository, entitlement, TenantModuleEntitlementRowActions.Disable, ct);
        if (refusal is not null)
        {
            return refusal;
        }

        var moduleCode = entitlement.ModuleCode;
        var auditBefore = PhysicalEntitlementAuditIntent.StateOf(entitlement);
        entitlement.IsEnabled = false;
        entitlement.Reason = request.Request.Reason;
        var auditIntentId = Guid.NewGuid();
        await _transactions.ExecuteAsync(async (session, transactionCt) =>
        {
            await _repository.UpdateAsync(session, entitlement, request.Request.RowVersion, transactionCt);
            var release = await ReleaseModuleQuotaAsync(session, request.TenantId, entitlement.Id, entitlement.RowVersion, moduleCode, request.Request.Reason, transactionCt);
            if (!release.IsSuccessful) throw new PhysicalEntitlementMutationRejectedException(release.Errors, release.StatusCode);
            await _versions.IncrementPhysicalEntitlementVersionAsync(session, request.TenantId, moduleCode, transactionCt);
            await EnqueueDisabledAsync(session, request.TenantId, moduleCode, transactionCt);
            await PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session, request.TenantId, Guid.NewGuid(),
                auditIntentId, nameof(DisableTenantModuleEntitlementCommand), AuditOperation.Deactivate,
                entitlement.Id, moduleCode, transactionCt,
                auditBefore, PhysicalEntitlementAuditIntent.StateOf(entitlement));
            return true;
        }, ct);
        return Response<NoContent>.Success(204);
    }

    /// <summary>
    /// The plan's own line, named by its module: suspending it writes the tenant's one manual override row for that
    /// module. Two screens doing this at once write ONE row — the unique (tenant, module, source) index refuses the
    /// second insert and the repository answers it as stale.
    /// </summary>
    private async Task<Response<NoContent>> SuspendPlanLineAsync(
        DisableTenantModuleEntitlementCommand request, string moduleCode, CancellationToken ct)
    {
        var refusal = await TenantModuleEntitlementActionGate.RefuseProjectionDisableAsync(
            _moduleRepository, _repository, _subscriptions, _plans, request.TenantId, moduleCode, ct);
        if (refusal is not null)
        {
            return refusal;
        }

        var newOverride = TenantModuleEntitlementCommandSupport.CreateManualOverride(request.TenantId, moduleCode, false, request.Request.Reason!);
        var newAuditIntentId = Guid.NewGuid();
        await _transactions.ExecuteAsync(async (session, transactionCt) =>
        {
            await _repository.CreateAsync(session, newOverride, transactionCt);
            await _versions.IncrementPhysicalEntitlementVersionAsync(session, request.TenantId, moduleCode, transactionCt);
            await EnqueueDisabledAsync(session, request.TenantId, moduleCode, transactionCt);
            await PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session, request.TenantId, Guid.NewGuid(),
                newAuditIntentId, nameof(DisableTenantModuleEntitlementCommand), AuditOperation.Deactivate,
                newOverride.Id, moduleCode, transactionCt,
                before: null, after: PhysicalEntitlementAuditIntent.StateOf(newOverride));
            return true;
        }, ct);
        return Response<NoContent>.Success(204);
    }

    private Task<Response<QuotaMutationDto>> ReleaseModuleQuotaAsync(IPlatformTransactionSession session, Guid tenantId, Guid entitlementId, byte[] rowVersion, string moduleCode, string? reason, CancellationToken ct) =>
        _quotaService.ReleaseEntitlementAsync(session, new ReleaseQuotaRequest(
            tenantId,
            QuotaKeys.ModulesMax,
            1,
            "ModuleEntitlement",
            // FIX-ENTITLEMENT-REENABLE — mirror the enable dedup fix: scope the release key to this disable EVENT
            // (the row's RowVersion) so repeated enable→disable cycles each release cleanly instead of the second
            // disable being rejected as a lifetime-duplicate operation.
            $"module-entitlement-disable:{entitlementId}:{Convert.ToHexString(rowVersion)}",
            moduleCode,
            reason ?? "Tenant module entitlement disabled.",
            null,
            Guid.NewGuid().ToString()), ct);

    private async Task EnqueueDisabledAsync(IPlatformTransactionSession session, Guid tenantId, string moduleCode, CancellationToken ct)
    {
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var actorId = _currentUser.UserId == Guid.Empty ? null : (Guid?)_currentUser.UserId;

        await _events.EnqueueAsync(
            session,
            new TenantEntitlementDisabledV1(
                eventId,
                occurredAtUtc,
                tenantId,
                correlationId,
                actorId,
                moduleCode),
            new EventPublishOptions
            {
                EventId = eventId,
                CorrelationId = correlationId,
                TenantId = tenantId,
                Producer = "Diten.Platform",
                OccurredAtUtc = occurredAtUtc
            },
            ct);
    }
}
