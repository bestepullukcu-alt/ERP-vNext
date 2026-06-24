using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Queries;

public sealed record GetQmsBaselineListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<QmsBaselineSummaryModel>>>;

public sealed record GetQmsBaselineByIdQuery(Guid BaselineReleaseId, string CorrelationId)
    : IRequest<Response<QmsBaselineSummaryModel>>;

public sealed record GetQmsBaselineDefinitionsQuery(Guid BaselineReleaseId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<QmsCollectionDefinitionModel>>>;

public sealed record GetQmsBaselineDefinitionByCanonicalIdQuery(Guid BaselineReleaseId, string CanonicalId, string CorrelationId)
    : IRequest<Response<QmsCollectionDefinitionModel>>;
