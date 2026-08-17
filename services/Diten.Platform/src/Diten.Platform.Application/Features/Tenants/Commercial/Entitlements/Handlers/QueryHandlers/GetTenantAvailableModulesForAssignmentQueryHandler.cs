using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Catalog;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;

public sealed class GetTenantAvailableModulesForAssignmentQueryHandler
    : IRequestHandler<GetTenantAvailableModulesForAssignmentQuery, Response<IReadOnlyList<TenantAvailableModuleDto>>>
{
    private readonly IPlatformCatalogContract _catalogContract;

    public GetTenantAvailableModulesForAssignmentQueryHandler(IPlatformCatalogContract catalogContract)
    {
        _catalogContract = catalogContract;
    }

    public async Task<Response<IReadOnlyList<TenantAvailableModuleDto>>> Handle(GetTenantAvailableModulesForAssignmentQuery request, CancellationToken ct)
    {
        var modules = await _catalogContract.GetAssignableModulesAsync(ct);
        var rows = modules
            .Select(x => new TenantAvailableModuleDto(x.ModuleCode, x.ModuleName, x.DisplayName))
            .ToList();
        return Response<IReadOnlyList<TenantAvailableModuleDto>>.Success(rows);
    }
}
