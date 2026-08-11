using Diten.PvgService.Application.RegPvBase;

namespace Diten.PvgService.Infrastructure.RegPvBase;

public sealed class DenyAllWorkflowTransitionGate : IPvgWorkflowTransitionGate
{
    public ValueTask<PvgPortDecision> EvaluateAsync(
        PvgWorkflowTransitionRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(PvgPortDecision.WorkflowTransitionDenied());
}
