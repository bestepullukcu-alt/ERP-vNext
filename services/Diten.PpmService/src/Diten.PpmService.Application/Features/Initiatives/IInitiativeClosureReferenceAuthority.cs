namespace Diten.PpmService.Application.Features.Initiatives;

public interface IInitiativeClosureReferenceAuthority
{
    Task<InitiativeAuthorityDisposition> ValidateEvidenceAsync(
        IReadOnlyList<Domain.Entities.InitiativeTypedReference> references, CancellationToken cancellationToken);
    Task<InitiativeAuthorityDisposition> ValidateFollowUpTasksAsync(
        IReadOnlyList<Domain.Entities.InitiativeTypedReference> references, CancellationToken cancellationToken);
}
