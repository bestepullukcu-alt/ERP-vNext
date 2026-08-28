using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQualityEvent.Queries;

// MOD-0029-FU22 — quality event / deviation / CAPA read queries (tenant-scoped; no side effects).

public sealed record GetDocumentQualityEventsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<QualityEventModel>>>;

public sealed record GetDocumentQualityEventByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<QualityEventModel>>;

public sealed record GetQualityEventSourceLinksQuery(Guid Id, string CorrelationId)
    : IRequest<Response<IReadOnlyList<QualityEventSourceLinkModel>>>;

public sealed record GetDocumentDeviationsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<DeviationModel>>>;

public sealed record GetDocumentDeviationByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<DeviationModel>>;

public sealed record GetDocumentCAPAActionsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<CapaActionModel>>>;

public sealed record GetDocumentCAPAActionByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<CapaActionModel>>;
