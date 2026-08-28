namespace Diten.PvgService.Application.RegPvBase;

public interface IPvgWorkflowTransitionGate
{
    ValueTask<PvgPortDecision> EvaluateAsync(
        PvgWorkflowTransitionRequest request,
        CancellationToken cancellationToken = default);
}
