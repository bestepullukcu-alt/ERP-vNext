using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleAssignments.Queries;
using Diten.Platform.Common.Catalog;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ModuleAssignments.Handlers.QueryHandlers;

public sealed class GetModuleTenantAssignmentDetailQueryHandler
    : IRequestHandler<GetModuleTenantAssignmentDetailQuery, Response<ModuleTenantAssignmentDetailDto>>
{
    private readonly IPlatformCatalogContract _catalogContract;
    private readonly ILogger<GetModuleTenantAssignmentDetailQueryHandler> _logger;

    public GetModuleTenantAssignmentDetailQueryHandler(
        IPlatformCatalogContract catalogContract,
        ILogger<GetModuleTenantAssignmentDetailQueryHandler> logger)
    {
        _catalogContract = catalogContract;
        _logger = logger;
    }

    public async Task<Response<ModuleTenantAssignmentDetailDto>> Handle(GetModuleTenantAssignmentDetailQuery request, CancellationToken ct)
    {
        var module = await ModuleAssignmentQueryHelpers.GetModuleAsync(_catalogContract, request.ModuleCode, ct);
        if (module is null)
        {
            return Response<ModuleTenantAssignmentDetailDto>.Fail("Module catalog item not found.", 404);
        }

        _logger.LogWarning(
            "module_assignment_dependency_failure_count Source={Source} ModuleCode={ModuleCode} TenantCode={TenantCode} CorrelationId={CorrelationId}",
            ModuleAssignmentQueryHelpers.TenantDependencySource,
            module.ModuleCode,
            request.TenantCode,
            request.CorrelationId);

        return Response<ModuleTenantAssignmentDetailDto>.Fail(
            "Tenant Module Assignment read source is not available yet.",
            503);
    }
}
