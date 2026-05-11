using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class RemoveTenantManualModuleOverrideCommandHandler : IRequestHandler<RemoveTenantManualModuleOverrideCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;

    public RemoveTenantManualModuleOverrideCommandHandler(ITenantModuleEntitlementRepository repository)
    {
        _repository = repository;
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
            await _repository.SoftDeleteAsync(request.TenantId, request.EntitlementId, request.Request.RowVersion, ct);
            return Response<NoContent>.Success(204);
        }
        catch (TenantModuleEntitlementConcurrencyException)
        {
            return TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
        }
    }
}
