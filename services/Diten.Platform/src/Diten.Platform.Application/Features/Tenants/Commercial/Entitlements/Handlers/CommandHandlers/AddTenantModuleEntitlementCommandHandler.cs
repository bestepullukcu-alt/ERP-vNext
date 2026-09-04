using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class AddTenantModuleEntitlementCommandHandler : IRequestHandler<AddTenantModuleEntitlementCommand, Response<Guid>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly IQuotaService _quotaService;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IEntitlementStateVersionRepository _versions;
    private readonly ITransactionalIntegrationEventWriter _events;
    private readonly ITransactionalAuditOutboxWriter _audit;
    private readonly ICurrentUserContext _currentUser;

    public AddTenantModuleEntitlementCommandHandler(
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

    public async Task<Response<Guid>> Handle(AddTenantModuleEntitlementCommand request, CancellationToken ct)
    {
        var moduleCode = TenantModuleEntitlementCommandSupport.NormalizeModuleCode(request.Request.ModuleCode);
        var moduleValidation = await TenantModuleEntitlementCommandSupport.ValidateModuleAsync(_moduleRepository, moduleCode, ct);
        if (!moduleValidation.IsValid)
        {
            return Response<Guid>.Fail(moduleValidation.Error!, moduleValidation.StatusCode);
        }

        var duplicate = await TenantModuleEntitlementCommandSupport.ValidateDuplicateAsync(
            _repository,
            request.TenantId,
            moduleCode,
            null,
            ct);
        if (!duplicate.IsValid)
        {
            return Response<Guid>.Fail(duplicate.Error!, duplicate.StatusCode);
        }

        var entitlement = new TenantModuleEntitlement
        {
            TenantId = request.TenantId,
            ModuleCode = moduleCode,
            Source = request.Request.Source,
            IsEnabled = request.Request.IsEnabled,
            ExpiryDateUtc = request.Request.ExpiryDateUtc,
            Reason = request.Request.Reason
        };

        try
        {
            var auditIntentId = Guid.NewGuid();
            await _transactions.ExecuteAsync(async (session, transactionCt) =>
            {
                if (entitlement.IsEnabled)
                {
                    await _quotaService.RecalculateEntitlementAsync(session, new RecalculateQuotaUsageRequest(
                        request.TenantId, QuotaKeys.ModulesMax, "ModuleEntitlement", null, moduleCode,
                        "Reconcile modules.max to the real enabled count before enforcing.", null,
                        Guid.NewGuid().ToString()), transactionCt);
                    var consume = await _quotaService.TryConsumeEntitlementAsync(session, new TryConsumeQuotaRequest(
                        request.TenantId, QuotaKeys.ModulesMax, 1, "ModuleEntitlement",
                        $"module-entitlement-add:{request.TenantId}:{moduleCode}:{request.Request.Source}", moduleCode,
                        request.Request.Reason ?? "Tenant module entitlement added.", null,
                        Guid.NewGuid().ToString()), transactionCt);
                    if (!consume.IsSuccessful)
                    {
                        throw new PhysicalEntitlementMutationRejectedException(consume.Errors, consume.StatusCode);
                    }
                }

                await _repository.CreateAsync(session, entitlement, transactionCt);
                await _versions.IncrementPhysicalEntitlementVersionAsync(session, request.TenantId, moduleCode, transactionCt);
                var eventId = Guid.NewGuid();
                var correlationId = Guid.NewGuid();
                var occurredAtUtc = DateTimeOffset.UtcNow;
                await _events.EnqueueAsync(session, new TenantEntitlementAddedV1(eventId, occurredAtUtc,
                        request.TenantId, correlationId,
                        _currentUser.UserId == Guid.Empty ? null : (Guid?)_currentUser.UserId, moduleCode),
                    new EventPublishOptions { EventId = eventId, CorrelationId = correlationId,
                        TenantId = request.TenantId, Producer = "Diten.Platform", OccurredAtUtc = occurredAtUtc }, transactionCt);
                await PhysicalEntitlementAuditIntent.EnqueueAsync(_audit, session, request.TenantId, correlationId,
                    auditIntentId, nameof(AddTenantModuleEntitlementCommand), AuditOperation.Assign,
                    entitlement.Id, moduleCode, transactionCt);
                return true;
            }, ct);
        }
        catch (PhysicalEntitlementMutationRejectedException exception)
        {
            return Response<Guid>.Fail(exception.Errors, exception.StatusCode);
        }

        return Response<Guid>.Success(entitlement.Id, 201);
    }
}
