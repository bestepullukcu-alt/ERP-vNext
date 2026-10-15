namespace Diten.PlanningService.Application.Features.DemandPlanning;

// A request header is only a selection hint. An independent, server-side assignment
// source must return the canonical LegalEntity for this tenant and actor.
// With no approved alias contract, the result must equal the selected GUID;
// consumers reject any mismatch. Null means no assignment; an outage must
// throw and fail closed.
public interface ILegalEntityAssignmentAuthority
{
    Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
        Guid selectedLegalEntityHint, CancellationToken cancellationToken);
}
