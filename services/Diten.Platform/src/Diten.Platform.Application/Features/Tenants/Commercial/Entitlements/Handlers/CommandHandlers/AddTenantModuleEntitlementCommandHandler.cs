using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class AddTenantModuleEntitlementCommandHandler : IRequestHandler<AddTenantModuleEntitlementCommand, Response<Guid>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly IQuotaService _quotaService;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserContext _currentUser;

    public AddTenantModuleEntitlementCommandHandler(
        ITenantModuleEntitlementRepository repository,
        IModuleCatalogRepository moduleRepository,
        IQuotaService quotaService,
        IEventBus eventBus,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _moduleRepository = moduleRepository;
        _quotaService = quotaService;
        _eventBus = eventBus;
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

        if (entitlement.IsEnabled)
        {
            var consume = await _quotaService.TryConsumeAsync(new TryConsumeQuotaRequest(
                request.TenantId,
                QuotaKeys.ModulesMax,
                1,
                "ModuleEntitlement",
                $"module-entitlement-add:{request.TenantId}:{moduleCode}:{request.Request.Source}",
                moduleCode,
                request.Request.Reason ?? "Tenant module entitlement added.",
                null,
                Guid.NewGuid().ToString()), ct);

            if (!consume.IsSuccessful)
            {
                return Response<Guid>.Fail(consume.Errors, consume.StatusCode);
            }
        }

        await _repository.CreateAsync(entitlement, ct);
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var actorId = _currentUser.UserId == Guid.Empty ? null : (Guid?)_currentUser.UserId;

        await _eventBus.PublishAsync(
            new TenantEntitlementAddedV1(
                eventId,
                occurredAtUtc,
                request.TenantId,
                correlationId,
                actorId,
                moduleCode),
            new EventPublishOptions
            {
                EventId = eventId,
                CorrelationId = correlationId,
                TenantId = request.TenantId,
                Producer = "Diten.Platform",
                OccurredAtUtc = occurredAtUtc
            },
            ct);

        return Response<Guid>.Success(entitlement.Id, 201);
    }
}
