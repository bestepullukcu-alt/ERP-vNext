using Diten.PpmService.Application.Features.Initiatives;
using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Infrastructure.Initiatives;

public sealed class UnavailableInitiativeClosureReferenceAuthority : IInitiativeClosureReferenceAuthority
{
    public Task<InitiativeAuthorityDisposition> ValidateEvidenceAsync(
        IReadOnlyList<InitiativeTypedReference> references, CancellationToken cancellationToken) =>
        Task.FromResult(references.Count == 0 ? InitiativeAuthorityDisposition.Valid : InitiativeAuthorityDisposition.Unavailable);

    public Task<InitiativeAuthorityDisposition> ValidateFollowUpTasksAsync(
        IReadOnlyList<InitiativeTypedReference> references, CancellationToken cancellationToken) =>
        Task.FromResult(references.Count == 0 ? InitiativeAuthorityDisposition.Valid : InitiativeAuthorityDisposition.Unavailable);
}
