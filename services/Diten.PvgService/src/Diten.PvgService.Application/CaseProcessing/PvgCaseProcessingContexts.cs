namespace Diten.PvgService.Application.CaseProcessing;

public sealed record PvgCaseProcessingServerTenantContext(string TenantId);

public sealed record PvgCaseProcessingActorContext(string ActorId, string ActorKind);

public sealed record PvgCaseProcessingCorrelationContext(string CorrelationId)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(CorrelationId) &&
        CorrelationId.Length <= 128 &&
        CorrelationId.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
}
