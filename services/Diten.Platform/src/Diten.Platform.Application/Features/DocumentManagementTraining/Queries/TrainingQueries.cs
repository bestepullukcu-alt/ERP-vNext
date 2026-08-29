using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTraining.Queries;

// MOD-0029-FU11 — training read queries (tenant-scoped; no side effects).

public sealed record GetTrainingRequirementsQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<TrainingRequirementModel>>>;

public sealed record GetTrainingReadinessQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<TrainingReadinessModel>>;
