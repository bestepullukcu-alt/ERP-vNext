using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Queries;

// MOD-0029-FU16 — repository assessment read queries (tenant-scoped; no side effects).

public sealed record GetRepositoryAssessmentsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<RepositoryAssessmentModel>>>;

public sealed record GetRepositoryAssessmentByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<RepositoryAssessmentModel>>;

public sealed record GetRepositoryAssessmentFindingsQuery(Guid Id, string CorrelationId)
    : IRequest<Response<IReadOnlyList<RepositoryAssessmentFindingModel>>>;

public sealed record GetLinkedRepositoryAssessmentQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<RepositoryAssessmentModel>>;
