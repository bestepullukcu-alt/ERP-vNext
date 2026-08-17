using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

public sealed class RefreshTenantModuleEntitlementProjectionCommandHandler : IRequestHandler<RefreshTenantModuleEntitlementProjectionCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(RefreshTenantModuleEntitlementProjectionCommand request, CancellationToken ct)
    {
        return Task.FromResult(Response<NoContent>.Success(204));
    }
}
