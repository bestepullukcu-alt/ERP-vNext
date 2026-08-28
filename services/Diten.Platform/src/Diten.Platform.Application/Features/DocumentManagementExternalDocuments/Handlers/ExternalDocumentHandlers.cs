using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Commands;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Queries;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Handlers;

// MOD-0029-FU14 — thin MediatR handlers delegating to ExternalDocumentRegisterService.

public sealed class CreateExternalDocumentRegisterEntryHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<CreateExternalDocumentRegisterEntryCommand, Response<ExternalDocumentModel>>
{
    public Task<Response<ExternalDocumentModel>> Handle(CreateExternalDocumentRegisterEntryCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class UpdateExternalDocumentRegisterEntryHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<UpdateExternalDocumentRegisterEntryCommand, Response<ExternalDocumentModel>>
{
    public Task<Response<ExternalDocumentModel>> Handle(UpdateExternalDocumentRegisterEntryCommand r, CancellationToken ct) =>
        s.UpdateAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class MarkExternalDocumentSupersededHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<MarkExternalDocumentSupersededCommand, Response<ExternalDocumentModel>>
{
    public Task<Response<ExternalDocumentModel>> Handle(MarkExternalDocumentSupersededCommand r, CancellationToken ct) =>
        s.MarkSupersededAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class ArchiveExternalDocumentHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<ArchiveExternalDocumentCommand, Response<ExternalDocumentModel>>
{
    public Task<Response<ExternalDocumentModel>> Handle(ArchiveExternalDocumentCommand r, CancellationToken ct) =>
        s.ArchiveAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class RecordExternalDocumentMonitoringCheckHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<RecordExternalDocumentMonitoringCheckCommand, Response<ExternalDocumentMonitoringCheckModel>>
{
    public Task<Response<ExternalDocumentMonitoringCheckModel>> Handle(RecordExternalDocumentMonitoringCheckCommand r, CancellationToken ct) =>
        s.RecordMonitoringCheckAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class CreateExternalDocumentImpactAssessmentHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<CreateExternalDocumentImpactAssessmentCommand, Response<ExternalDocumentImpactAssessmentModel>>
{
    public Task<Response<ExternalDocumentImpactAssessmentModel>> Handle(CreateExternalDocumentImpactAssessmentCommand r, CancellationToken ct) =>
        s.CreateImpactAssessmentAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class CompleteExternalDocumentImpactAssessmentHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<CompleteExternalDocumentImpactAssessmentCommand, Response<ExternalDocumentImpactAssessmentModel>>
{
    public Task<Response<ExternalDocumentImpactAssessmentModel>> Handle(CompleteExternalDocumentImpactAssessmentCommand r, CancellationToken ct) =>
        s.CompleteImpactAssessmentAsync(r.Id, r.AssessmentId, r.Input, r.CorrelationId, ct);
}

public sealed class LinkExternalDocumentToInternalRegisterEntryHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<LinkExternalDocumentToInternalRegisterEntryCommand, Response<ExternalDocumentInternalLinkModel>>
{
    public Task<Response<ExternalDocumentInternalLinkModel>> Handle(LinkExternalDocumentToInternalRegisterEntryCommand r, CancellationToken ct) =>
        s.LinkToInternalRegisterAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class CloseExternalDocumentInternalLinkHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<CloseExternalDocumentInternalLinkCommand, Response<ExternalDocumentInternalLinkModel>>
{
    public Task<Response<ExternalDocumentInternalLinkModel>> Handle(CloseExternalDocumentInternalLinkCommand r, CancellationToken ct) =>
        s.CloseInternalLinkAsync(r.Id, r.LinkId, r.CorrelationId, ct);
}

public sealed class GetExternalDocumentsHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<GetExternalDocumentsQuery, Response<IReadOnlyList<ExternalDocumentModel>>>
{
    public Task<Response<IReadOnlyList<ExternalDocumentModel>>> Handle(GetExternalDocumentsQuery r, CancellationToken ct) =>
        s.ListAsync(r.Filter, r.CorrelationId, ct);
}

public sealed class GetExternalDocumentByIdHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<GetExternalDocumentByIdQuery, Response<ExternalDocumentModel>>
{
    public Task<Response<ExternalDocumentModel>> Handle(GetExternalDocumentByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetExternalDocumentMonitoringChecksHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<GetExternalDocumentMonitoringChecksQuery, Response<IReadOnlyList<ExternalDocumentMonitoringCheckModel>>>
{
    public Task<Response<IReadOnlyList<ExternalDocumentMonitoringCheckModel>>> Handle(GetExternalDocumentMonitoringChecksQuery r, CancellationToken ct) =>
        s.GetMonitoringChecksAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetExternalDocumentsMonitoringDueHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<GetExternalDocumentsMonitoringDueQuery, Response<IReadOnlyList<ExternalDocumentMonitoringDueModel>>>
{
    public Task<Response<IReadOnlyList<ExternalDocumentMonitoringDueModel>>> Handle(GetExternalDocumentsMonitoringDueQuery r, CancellationToken ct) =>
        s.GetMonitoringDueAsync(r.CorrelationId, ct);
}

public sealed class GetExternalDocumentImpactAssessmentsHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<GetExternalDocumentImpactAssessmentsQuery, Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>>
{
    public Task<Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>> Handle(GetExternalDocumentImpactAssessmentsQuery r, CancellationToken ct) =>
        s.GetImpactAssessmentsAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetOverdueExternalDocumentImpactAssessmentsHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<GetOverdueExternalDocumentImpactAssessmentsQuery, Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>>
{
    public Task<Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>> Handle(GetOverdueExternalDocumentImpactAssessmentsQuery r, CancellationToken ct) =>
        s.GetOverdueImpactAssessmentsAsync(r.CorrelationId, ct);
}

public sealed class GetExternalDocumentInternalLinksHandler(ExternalDocumentRegisterService s)
    : IRequestHandler<GetExternalDocumentInternalLinksQuery, Response<IReadOnlyList<ExternalDocumentInternalLinkModel>>>
{
    public Task<Response<IReadOnlyList<ExternalDocumentInternalLinkModel>>> Handle(GetExternalDocumentInternalLinksQuery r, CancellationToken ct) =>
        s.GetInternalLinksAsync(r.Id, r.CorrelationId, ct);
}
