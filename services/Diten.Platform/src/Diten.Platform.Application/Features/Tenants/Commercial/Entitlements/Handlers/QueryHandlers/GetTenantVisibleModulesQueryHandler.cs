using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Application.Services;
using Diten.Platform.Common.Catalog;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;

public sealed class GetTenantVisibleModulesQueryHandler
    : IRequestHandler<GetTenantVisibleModulesQuery, Response<IReadOnlyList<TenantVisibleModuleDto>>>
{
    private readonly IPlatformCatalogContract _catalogContract;
    private readonly ITenantModuleAccessService _accessService;

    public GetTenantVisibleModulesQueryHandler(IPlatformCatalogContract catalogContract, ITenantModuleAccessService accessService)
    {
        _catalogContract = catalogContract;
        _accessService = accessService;
    }

    public async Task<Response<IReadOnlyList<TenantVisibleModuleDto>>> Handle(GetTenantVisibleModulesQuery request, CancellationToken ct)
    {
        var modules = await _catalogContract.GetAssignableModulesAsync(ct);
        var rows = new List<TenantVisibleModuleDto>();
        foreach (var module in modules)
        {
            if (await _accessService.HasAccessAsync(request.TenantId, module.ModuleCode, ct))
            {
                rows.Add(new TenantVisibleModuleDto(module.ModuleCode, module.ModuleName, module.DisplayName, module.Description));
            }
        }

        return Response<IReadOnlyList<TenantVisibleModuleDto>>.Success(rows);
    }
}
