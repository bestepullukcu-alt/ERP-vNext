using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;

public sealed record ProcessModelGraphContract(
    Guid TenantId, Guid ModelId, int RevisionNumber, ProcessModelVersionState State,
    IReadOnlyList<ProcessActivity> Activities,
    IReadOnlyList<ProcessControlPoint> ControlPoints,
    IReadOnlyList<ProcessRelationship> Relationships,
    string ContentHash);
