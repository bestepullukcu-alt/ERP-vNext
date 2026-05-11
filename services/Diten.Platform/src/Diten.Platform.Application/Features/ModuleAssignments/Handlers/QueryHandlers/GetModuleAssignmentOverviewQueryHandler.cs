using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleAssignments.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ModuleAssignments.Handlers.QueryHandlers;

public sealed class GetModuleAssignmentOverviewQueryHandler
    : IRequestHandler<GetModuleAssignmentOverviewQuery, Response<ModuleAssignmentOverviewDto>>
{
    private readonly IModuleCatalogRepository _moduleRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ILogger<GetModuleAssignmentOverviewQueryHandler> _logger;

    public GetModuleAssignmentOverviewQueryHandler(
        IModuleCatalogRepository moduleRepository,
        ISubscriptionPlanRepository planRepository,
        ILogger<GetModuleAssignmentOverviewQueryHandler> logger)
    {
        _moduleRepository = moduleRepository;
        _planRepository = planRepository;
        _logger = logger;
    }

    public async Task<Response<ModuleAssignmentOverviewDto>> Handle(GetModuleAssignmentOverviewQuery request, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var module = await ModuleAssignmentQueryHelpers.GetModuleAsync(_moduleRepository, request.ModuleCode, ct);
        if (module is null)
        {
            return Response<ModuleAssignmentOverviewDto>.Fail("Module catalog item not found.", 404);
        }

        var plans = await _planRepository.GetByIncludedModuleKeyAsync(module.ModuleCode, ct);
        DateTimeOffset? latestPlanUpdate = plans.Count == 0
            ? null
            : plans.Max(x => x.UpdatedAt ?? x.CreatedAt);
        var dependencies = new[] { ModuleAssignmentQueryHelpers.TenantDependencyUnavailable() };

        var dto = new ModuleAssignmentOverviewDto(
            module.ModuleCode,
            module.ModuleName,
            module.Status.ToString(),
            plans.Count,
            null,
            null,
            null,
            null,
            null,
            latestPlanUpdate,
            dependencies,
            request.CorrelationId);

        _logger.LogInformation(
            "module_assignment_overview_load_duration ModuleCode={ModuleCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
            module.ModuleCode,
            (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            request.CorrelationId);

        return Response<ModuleAssignmentOverviewDto>.Success(dto);
    }
}
