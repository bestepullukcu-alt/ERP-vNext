using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitWorkspace;

/// <summary>
/// WP-VW-W2 (A4) — where the workspace calendar's DRAFT-week visits come from: the EXISTING planning preview (no new
/// calculation). A seam only so the calendar read can be measured without the whole planning engine.
/// </summary>
public interface IWorkspacePlanPreviewSource
{
    /// <summary>The session's preview, or null when the engine cannot preview it (the calendar then shows no draft).</summary>
    Task<VisitPlanPreview?> PreviewAsync(PlanningSession session, CancellationToken cancellationToken);
}

/// <summary>The production source: <see cref="VisitPlanningEngine.PreviewAsync"/> with the default options (persists
/// nothing).</summary>
public sealed class EngineWorkspacePlanPreviewSource : IWorkspacePlanPreviewSource
{
    private readonly VisitPlanningEngine _engine;

    public EngineWorkspacePlanPreviewSource(VisitPlanningEngine engine) => _engine = engine;

    public async Task<VisitPlanPreview?> PreviewAsync(PlanningSession session, CancellationToken cancellationToken)
    {
        var outcome = await _engine.PreviewAsync(session, new VisitPlanGenerationOptions(), cancellationToken);
        return outcome.Success ? outcome.Preview : null;
    }
}
