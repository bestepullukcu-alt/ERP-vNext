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
    private readonly IQuotaService _quotaService;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IEntitlementStateVersionRepository _versions;
    private readonly ITransactionalIntegrationEventWriter _events;
    private readonly ITransactionalAuditOutboxWriter _audit;
    private readonly ICurrentUserContext _currentUser;

    public DisableTenantModuleEntitlementCommandHandler(
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

    public async Task<Response<NoContent>> Handle(DisableTenantModuleEntitlementCommand request, CancellationToken ct)
    {
        var moduleCode = TenantModuleEntitlementCommandSupport.NormalizeModuleCode(request.Request.ModuleCode);
        var module = await _moduleRepository.GetByCodeAsync(moduleCode, ct);
        if (module is null)
        {
            return Response<NoContent>.Fail("Module was not found.", 404);
        }

        if (module.IsCoreModule)
        {
            return Response<NoContent>.Fail("Core system modules cannot be disabled.", 409);
        }

        // FEAT-BASELINE-MODULES — a baseline module is entitlement-free (every tenant auto-has it); disabling it via a
        // manual override is meaningless (access checks bypass entitlements for baseline) and misleading, so reject.
        if (module.IsBaseline)
        {
            return Response<NoContent>.Fail("Baseline modules are entitlement-free and cannot be disabled.", 409);
        }

        try
        {
            if (request.Request.PhysicalEntitlementId.HasValue)
            {
                var entitlement = await _repository.GetByIdAsync(request.TenantId, request.Request.PhysicalEntitlementId.Value, ct);
                if (entitlement is null)
                {
                    return Response<NoContent>.Fail("Entitlement was not found.", 404);
                }

                var wasEnabled = entitlement.IsEnabled;
                if (!wasEnabled && string.Equals(entitlement.Reason, request.Request.Reason, StringComparison.Ordinal))
                {
                    return Response<NoContent>.Success(204);
                }
                entitlement.IsEnabled = false;
                entitlement.Reason = request.Request.Reason;
                var auditIntentId = Guid.NewGuid();
                await _transactions.ExecuteAsync(async (session, transactionCt) =>
                {
                    await _repository.UpdateAsync(session, entitlement, request.Request.RowVersion, transactionCt);
                    if (wasEnabled)
                    {
                        var release = await ReleaseModuleQuotaAsync(session, request.TenantId, entitlement.Id, entitlement.RowVersion, moduleCode, request.Request.Reason, transactionCt);
                        if (!release.IsSuccessful) throw new PhysicalEntitlementMutationRejectedException(release.Errors, release.StatusCode);
                    }
                    await _versions.IncrementPhysicalEntitlementVersionAsync(session, request.TenantId, entitlement.ModuleCode, transactionCt);
                    await EnqueueDisabledAsync(session, request.TenantId, entitlement.ModuleCode, transactionCt);
                    await PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session, request.TenantId, Guid.NewGuid(),
                        auditIntentId, nameof(DisableTenantModuleEntitlementCommand), AuditOperation.Deactivate,
                        entitlement.Id, entitlement.ModuleCode, transactionCt);
                    return true;
                }, ct);
                return Response<NoContent>.Success(204);
            }

            var existingOverride = await _repository.GetActiveBySourceAsync(request.TenantId, moduleCode, EntitlementSource.ManualOverride, null, ct);
            if (existingOverride is not null)
            {
                var wasEnabled = existingOverride.IsEnabled;
                if (!wasEnabled && string.Equals(existingOverride.Reason, request.Request.Reason, StringComparison.Ordinal))
                {
                    return Response<NoContent>.Success(204);
                }
                existingOverride.IsEnabled = false;
                existingOverride.Reason = request.Request.Reason;
                var auditIntentId = Guid.NewGuid();
                await _transactions.ExecuteAsync(async (session, transactionCt) =>
                {
                    await _repository.UpdateAsync(session, existingOverride, request.Request.RowVersion, transactionCt);
                    if (wasEnabled)
                    {
                        var release = await ReleaseModuleQuotaAsync(session, request.TenantId, existingOverride.Id, existingOverride.RowVersion, moduleCode, request.Request.Reason, transactionCt);
                        if (!release.IsSuccessful) throw new PhysicalEntitlementMutationRejectedException(release.Errors, release.StatusCode);
                    }
                    await _versions.IncrementPhysicalEntitlementVersionAsync(session, request.TenantId, existingOverride.ModuleCode, transactionCt);
                    await EnqueueDisabledAsync(session, request.TenantId, existingOverride.ModuleCode, transactionCt);
                    await PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session, request.TenantId, Guid.NewGuid(),
                        auditIntentId, nameof(DisableTenantModuleEntitlementCommand), AuditOperation.Deactivate,
                        existingOverride.Id, existingOverride.ModuleCode, transactionCt);
                    return true;
                }, ct);
                return Response<NoContent>.Success(204);
            }

            var newOverride = TenantModuleEntitlementCommandSupport.CreateManualOverride(request.TenantId, moduleCode, false, request.Request.Reason);
            var newAuditIntentId = Guid.NewGuid();
            await _transactions.ExecuteAsync(async (session, transactionCt) =>
            {
                await _repository.CreateAsync(session, newOverride, transactionCt);
                await _versions.IncrementPhysicalEntitlementVersionAsync(session, request.TenantId, moduleCode, transactionCt);
                await EnqueueDisabledAsync(session, request.TenantId, moduleCode, transactionCt);
                await PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session, request.TenantId, Guid.NewGuid(),
                    newAuditIntentId, nameof(DisableTenantModuleEntitlementCommand), AuditOperation.Deactivate,
                    newOverride.Id, moduleCode, transactionCt);
                return true;
            }, ct);
            return Response<NoContent>.Success(204);
        }
        catch (PhysicalEntitlementMutationRejectedException exception)
        {
            return Response<NoContent>.Fail(exception.Errors, exception.StatusCode);
        }
        catch (TenantModuleEntitlementConcurrencyException)
        {
            return TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
        }
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
