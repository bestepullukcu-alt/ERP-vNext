namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingUserAcceptance;

public sealed record ProcessModelSummary(Guid Id, Guid ProcessDefinitionId, string ModelCode, string Name,
    string? Description, int LatestRevisionNumber, Guid? PublishedVersionId, Guid? OpenVersionId, int Version);

public interface IProcessModelUserAcceptanceQueries
{
    Task<IReadOnlyList<ProcessModelSummary>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ProcessModelSummary?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}
