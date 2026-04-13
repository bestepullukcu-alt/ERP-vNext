using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Domain.Aggregates.EnterpriseStrategy;

namespace Diten.Application.EnterpriseStrategy.Services;

/// <summary>Persists a runtime goal definition as a reusable Goal library template.</summary>
public interface IGoalTemplateSnapshotWriter
{
    Task<string?> WriteFromGoalAsync(GoalAggregate goal, GoalTemplateSaveMetadataDto metadata, string actor, CancellationToken cancellationToken = default);
}
