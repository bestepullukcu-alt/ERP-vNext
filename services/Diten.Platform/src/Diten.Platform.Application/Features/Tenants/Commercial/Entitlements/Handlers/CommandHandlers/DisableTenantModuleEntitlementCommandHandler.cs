using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class DisableTenantModuleEntitlementCommandHandler : IRequestHandler<DisableTenantModuleEntitlementCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly IQuotaService _quotaService;

    public DisableTenantModuleEntitlementCommandHandler(
        ITenantModuleEntitlementRepository repository,
        IModuleCatalogRepository moduleRepository,
        IQuotaService quotaService)
    {
        _repository = repository;
        _moduleRepository = moduleRepository;
        _quotaService = quotaService;
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
                entitlement.IsEnabled = false;
                entitlement.Reason = request.Request.Reason;
                await _repository.UpdateAsync(entitlement, request.Request.RowVersion, ct);
                if (wasEnabled)
                {
                    var release = await ReleaseModuleQuotaAsync(request.TenantId, entitlement.Id, moduleCode, request.Request.Reason, ct);
                    if (!release.IsSuccessful)
                    {
                        return Response<NoContent>.Fail(release.Errors, release.StatusCode);
                    }
                }
                return Response<NoContent>.Success(204);
            }

            var existingOverride = await _repository.GetActiveBySourceAsync(request.TenantId, moduleCode, EntitlementSource.ManualOverride, null, ct);
            if (existingOverride is not null)
            {
                var wasEnabled = existingOverride.IsEnabled;
                existingOverride.IsEnabled = false;
                existingOverride.Reason = request.Request.Reason;
                await _repository.UpdateAsync(existingOverride, request.Request.RowVersion, ct);
                if (wasEnabled)
                {
                    var release = await ReleaseModuleQuotaAsync(request.TenantId, existingOverride.Id, moduleCode, request.Request.Reason, ct);
                    if (!release.IsSuccessful)
                    {
                        return Response<NoContent>.Fail(release.Errors, release.StatusCode);
                    }
                }
                return Response<NoContent>.Success(204);
            }

            await _repository.CreateAsync(TenantModuleEntitlementCommandSupport.CreateManualOverride(request.TenantId, moduleCode, false, request.Request.Reason), ct);
            return Response<NoContent>.Success(204);
        }
        catch (TenantModuleEntitlementConcurrencyException)
        {
            return TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
        }
    }

    private Task<Response<QuotaMutationDto>> ReleaseModuleQuotaAsync(Guid tenantId, Guid entitlementId, string moduleCode, string? reason, CancellationToken ct) =>
        _quotaService.ReleaseAsync(new ReleaseQuotaRequest(
            tenantId,
            QuotaKeys.ModulesMax,
            1,
            "ModuleEntitlement",
            $"module-entitlement-disable:{entitlementId}",
            moduleCode,
            reason ?? "Tenant module entitlement disabled.",
            null,
            Guid.NewGuid().ToString()), ct);
}
