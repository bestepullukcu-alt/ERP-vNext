using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;

public interface IInstantiationPlanner
{
    Task<Response<InstantiationPlan>> PlanAsync(
        Guid baselineReleaseId,
        InstantiationScopeRequest scope,
        InstantiationSelectionRequest selection,
        string correlationId,
        CancellationToken ct = default);
}
