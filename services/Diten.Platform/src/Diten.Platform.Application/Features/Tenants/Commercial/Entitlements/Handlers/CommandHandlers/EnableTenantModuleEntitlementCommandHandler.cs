using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class EnableTenantModuleEntitlementCommandHandler : IRequestHandler<EnableTenantModuleEntitlementCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;
    private readonly IQuotaService _quotaService;

    public EnableTenantModuleEntitlementCommandHandler(ITenantModuleEntitlementRepository repository, IQuotaService quotaService)
    {
        _repository = repository;
        _quotaService = quotaService;
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
            if (!entitlement.IsEnabled)
            {
                var consume = await _quotaService.TryConsumeAsync(new TryConsumeQuotaRequest(
                    request.TenantId,
                    QuotaKeys.ModulesMax,
                    1,
                    "ModuleEntitlement",
                    $"module-entitlement-enable:{request.EntitlementId}",
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
            return Response<NoContent>.Success(204);
        }
        catch (TenantModuleEntitlementConcurrencyException)
        {
            return TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
        }
    }
}
