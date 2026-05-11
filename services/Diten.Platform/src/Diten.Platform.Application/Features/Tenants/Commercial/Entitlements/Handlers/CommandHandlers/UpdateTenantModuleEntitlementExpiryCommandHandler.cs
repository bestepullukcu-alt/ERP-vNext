using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class UpdateTenantModuleEntitlementExpiryCommandHandler : IRequestHandler<UpdateTenantModuleEntitlementExpiryCommand, Response<NoContent>>
{
    private readonly ITenantModuleEntitlementRepository _repository;

    public UpdateTenantModuleEntitlementExpiryCommandHandler(ITenantModuleEntitlementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(UpdateTenantModuleEntitlementExpiryCommand request, CancellationToken ct)
    {
        var entitlement = await _repository.GetByIdAsync(request.TenantId, request.EntitlementId, ct);
        if (entitlement is null)
        {
            return Response<NoContent>.Fail("Entitlement was not found.", 404);
        }

        try
        {
            entitlement.ExpiryDateUtc = request.Request.ExpiryDateUtc;
            if (!string.IsNullOrWhiteSpace(request.Request.Reason))
            {
                entitlement.Reason = request.Request.Reason;
            }

            await _repository.UpdateAsync(entitlement, request.Request.RowVersion, ct);
            return Response<NoContent>.Success(204);
        }
        catch (TenantModuleEntitlementConcurrencyException)
        {
            return TenantModuleEntitlementCommandSupport.ConcurrencyFailure();
        }
    }
}
