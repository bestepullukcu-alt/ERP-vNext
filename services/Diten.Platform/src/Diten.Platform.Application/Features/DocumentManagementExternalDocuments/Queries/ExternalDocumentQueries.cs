using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Queries;

// MOD-0029-FU14 — external document register read queries (tenant-scoped). The overdue-impact query is the one
// exception to "no side effects": it PERSISTS the Overdue status so the register reflects the missed deadline.

public sealed record GetExternalDocumentsQuery(ExternalDocumentListFilter Filter, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ExternalDocumentModel>>>;

public sealed record GetExternalDocumentByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<ExternalDocumentModel>>;

public sealed record GetExternalDocumentMonitoringChecksQuery(Guid Id, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ExternalDocumentMonitoringCheckModel>>>;

public sealed record GetExternalDocumentsMonitoringDueQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<ExternalDocumentMonitoringDueModel>>>;

public sealed record GetExternalDocumentImpactAssessmentsQuery(Guid Id, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>>;

public sealed record GetOverdueExternalDocumentImpactAssessmentsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>>;

public sealed record GetExternalDocumentInternalLinksQuery(Guid Id, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ExternalDocumentInternalLinkModel>>>;
