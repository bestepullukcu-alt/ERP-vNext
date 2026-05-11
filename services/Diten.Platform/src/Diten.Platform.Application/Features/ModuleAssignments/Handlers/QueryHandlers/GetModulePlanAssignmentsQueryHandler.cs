using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleAssignments.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ModuleAssignments.Handlers.QueryHandlers;

public sealed class GetModulePlanAssignmentsQueryHandler
    : IRequestHandler<GetModulePlanAssignmentsQuery, Response<ModuleAssignmentPageDto<ModulePlanAssignmentRowDto>>>
{
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ILogger<GetModulePlanAssignmentsQueryHandler> _logger;

    public GetModulePlanAssignmentsQueryHandler(
        IModuleCatalogRepository moduleRepository,
        ISubscriptionPlanRepository planRepository,
        ILogger<GetModulePlanAssignmentsQueryHandler> logger)
    {
        _moduleRepository = moduleRepository;
        _planRepository = planRepository;
        _logger = logger;
    }

    public async Task<Response<ModuleAssignmentPageDto<ModulePlanAssignmentRowDto>>> Handle(GetModulePlanAssignmentsQuery request, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var module = await ModuleAssignmentQueryHelpers.GetModuleAsync(_moduleRepository, request.ModuleCode, ct);
        if (module is null)
        {
            return Response<ModuleAssignmentPageDto<ModulePlanAssignmentRowDto>>.Fail("Module catalog item not found.", 404);
        }

        var plans = await _planRepository.GetByIncludedModuleKeyAsync(module.ModuleCode, ct);
        var rows = plans.Select(ModuleAssignmentQueryHelpers.ToPlanRow).ToList();
        rows = ModuleAssignmentQueryHelpers.ApplyPlanFilters(rows, request.Filter).ToList();
        var page = ModuleAssignmentPaging.ToPage(rows, request.Filter.Page, request.Filter.PageSize);

        _logger.LogInformation(
            "module_assignment_plan_query_duration ModuleCode={ModuleCode} DurationMs={DurationMs}",
            module.ModuleCode,
            (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);

        return Response<ModuleAssignmentPageDto<ModulePlanAssignmentRowDto>>.Success(page);
    }
}
