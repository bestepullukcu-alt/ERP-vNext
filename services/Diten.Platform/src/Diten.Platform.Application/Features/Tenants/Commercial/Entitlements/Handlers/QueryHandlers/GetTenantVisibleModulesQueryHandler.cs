using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;

public sealed class GetTenantVisibleModulesQueryHandler
    : IRequestHandler<GetTenantVisibleModulesQuery, Response<IReadOnlyList<TenantVisibleModuleDto>>>
{
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly ITenantModuleAccessService _accessService;

    public GetTenantVisibleModulesQueryHandler(IModuleCatalogRepository moduleRepository, ITenantModuleAccessService accessService)
    {
        _moduleRepository = moduleRepository;
        _accessService = accessService;
    }

    public async Task<Response<IReadOnlyList<TenantVisibleModuleDto>>> Handle(GetTenantVisibleModulesQuery request, CancellationToken ct)
    {
        var modules = await _moduleRepository.GetAssignableAsync(ct);
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
