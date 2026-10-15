using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

// The header only selects a company. The existing server-side assignment seam
// must independently resolve it, and no alias mapping has been approved.
internal static class RevisionCommandScope
{
    public static async Task<(Guid LegalEntityId, int ErrorStatus)> ResolveAsync(
        IManualDraftAuthority authority, Guid tenantId, Guid actorId,
        Guid selectedHint, CancellationToken cancellationToken)
    {
        if (selectedHint == Guid.Empty) return (Guid.Empty, 400);
        try
        {
            var resolved = await authority.ResolveSelectedAsync(tenantId,
                actorId, selectedHint, cancellationToken);
            return resolved == selectedHint
                ? (resolved.Value, 0)
                : (Guid.Empty, 404);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return (Guid.Empty, 503);
        }
    }
}
