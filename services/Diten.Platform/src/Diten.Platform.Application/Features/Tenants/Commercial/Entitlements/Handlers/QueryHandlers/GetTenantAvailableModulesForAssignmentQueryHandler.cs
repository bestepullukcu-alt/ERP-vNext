using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;

public sealed class GetTenantAvailableModulesForAssignmentQueryHandler
    : IRequestHandler<GetTenantAvailableModulesForAssignmentQuery, Response<IReadOnlyList<TenantAvailableModuleDto>>>
{
    private readonly IModuleCatalogRepository _moduleRepository;

    public GetTenantAvailableModulesForAssignmentQueryHandler(IModuleCatalogRepository moduleRepository)
    {
        _moduleRepository = moduleRepository;
    }

    public async Task<Response<IReadOnlyList<TenantAvailableModuleDto>>> Handle(GetTenantAvailableModulesForAssignmentQuery request, CancellationToken ct)
    {
        var modules = await _moduleRepository.GetAssignableAsync(ct);
        var rows = modules
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ModuleCode)
            .Select(x => new TenantAvailableModuleDto(x.ModuleCode, x.ModuleName, x.DisplayName))
            .ToList();
        return Response<IReadOnlyList<TenantAvailableModuleDto>>.Success(rows);
    }
}
