using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class RemoveTenantManualModuleOverrideCommandHandler : IRequestHandler<RemoveTenantManualModuleOverrideCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IQuotaService _quotaService;

    public RemoveTenantManualModuleOverrideCommandHandler(ITenantModuleEntitlementRepository repository, IQuotaService quotaService)
    {
        _repository = repository;
        _quotaService = quotaService;
    }

    public async Task<Response<NoContent>> Handle(RemoveTenantManualModuleOverrideCommand request, CancellationToken ct)
    {
        var entitlement = await _repository.GetByIdAsync(request.TenantId, request.EntitlementId, ct);
        if (entitlement is null)
        {
            return Response<NoContent>.Fail("Entitlement was not found.", 404);
        }

        if (entitlement.Source != EntitlementSource.ManualOverride)
        {
            return Response<NoContent>.Fail("Only manual overrides can be removed.", 409);
        }

        try
        {
            var wasEnabled = entitlement.IsEnabled;
            await _repository.SoftDeleteAsync(request.TenantId, request.EntitlementId, request.Request.RowVersion, ct);
            if (wasEnabled)
            {
                var release = await _quotaService.ReleaseAsync(new ReleaseQuotaRequest(
                    request.TenantId,
                    QuotaKeys.ModulesMax,
                    1,
                    "ModuleEntitlement",
                    $"module-entitlement-remove:{request.EntitlementId}",
                    entitlement.ModuleCode,
                    "Tenant manual module override removed.",
                    null,
                    Guid.NewGuid().ToString()), ct);

                if (!release.IsSuccessful)
                {
                    return Response<NoContent>.Fail(release.Errors, release.StatusCode);
                }
            }

            return Response<NoContent>.Success(204);
        }
        catch (TenantModuleEntitlementConcurrencyException)
        {
            return TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
        }
    }
}
