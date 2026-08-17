using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Application.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;

public sealed class GetTenantModuleEffectiveAccessQueryHandler
    : IRequestHandler<GetTenantModuleEffectiveAccessQuery, Response<TenantModuleEffectiveAccessDto>>
{
    private readonly ITenantModuleAccessService _accessService;

    public GetTenantModuleEffectiveAccessQueryHandler(ITenantModuleAccessService accessService)
    {
        _accessService = accessService;
    }

    public async Task<Response<TenantModuleEffectiveAccessDto>> Handle(GetTenantModuleEffectiveAccessQuery request, CancellationToken ct)
    {
        var result = await _accessService.GetEffectiveAccessDetailAsync(request.TenantId, request.ModuleCode, ct);
        return Response<TenantModuleEffectiveAccessDto>.Success(result);
    }
}
