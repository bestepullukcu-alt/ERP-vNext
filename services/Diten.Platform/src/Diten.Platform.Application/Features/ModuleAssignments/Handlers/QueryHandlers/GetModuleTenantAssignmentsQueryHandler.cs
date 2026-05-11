using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleAssignments.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ModuleAssignments.Handlers.QueryHandlers;

public sealed class GetModuleTenantAssignmentsQueryHandler
    : IRequestHandler<GetModuleTenantAssignmentsQuery, Response<ModuleAssignmentPageDto<ModuleTenantAssignmentRowDto>>>
{
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly ILogger<GetModuleTenantAssignmentsQueryHandler> _logger;

    public GetModuleTenantAssignmentsQueryHandler(
        IModuleCatalogRepository moduleRepository,
        ILogger<GetModuleTenantAssignmentsQueryHandler> logger)
    {
        _moduleRepository = moduleRepository;
        _logger = logger;
    }

    public async Task<Response<ModuleAssignmentPageDto<ModuleTenantAssignmentRowDto>>> Handle(GetModuleTenantAssignmentsQuery request, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        if (!ModuleAssignmentQueryHelpers.IsValidTenantSource(request.Filter.Source))
        {
            return Response<ModuleAssignmentPageDto<ModuleTenantAssignmentRowDto>>.Fail("Invalid assignment source filter.", 400);
        }

        if (!ModuleAssignmentQueryHelpers.IsValidTenantStatus(request.Filter.Status))
        {
            return Response<ModuleAssignmentPageDto<ModuleTenantAssignmentRowDto>>.Fail("Invalid assignment status filter.", 400);
        }

        var module = await ModuleAssignmentQueryHelpers.GetModuleAsync(_moduleRepository, request.ModuleCode, ct);
        if (module is null)
        {
            return Response<ModuleAssignmentPageDto<ModuleTenantAssignmentRowDto>>.Fail("Module catalog item not found.", 404);
        }

        var dependency = ModuleAssignmentQueryHelpers.TenantDependencyUnavailable();
        var page = ModuleAssignmentPaging.ToPage(
            Array.Empty<ModuleTenantAssignmentRowDto>(),
            request.Filter.Page,
            request.Filter.PageSize,
            [dependency]);

        _logger.LogWarning(
            "module_assignment_dependency_failure_count Source={Source} ModuleCode={ModuleCode} CorrelationId={CorrelationId}",
            dependency.Source,
            module.ModuleCode,
            string.Empty);
        _logger.LogInformation(
            "module_assignment_tenant_query_duration ModuleCode={ModuleCode} DurationMs={DurationMs}",
            module.ModuleCode,
            (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);

        return Response<ModuleAssignmentPageDto<ModuleTenantAssignmentRowDto>>.Success(page);
    }
}
