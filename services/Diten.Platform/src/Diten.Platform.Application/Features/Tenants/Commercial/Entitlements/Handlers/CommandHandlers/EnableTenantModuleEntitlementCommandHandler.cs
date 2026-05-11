using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class EnableTenantModuleEntitlementCommandHandler : IRequestHandler<EnableTenantModuleEntitlementCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;

    public EnableTenantModuleEntitlementCommandHandler(ITenantModuleEntitlementRepository repository)
    {
        _repository = repository;
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
