using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent.Commands;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent.Queries;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQualityEvent.Handlers;

// MOD-0029-FU22 — thin MediatR handlers delegating to the quality event / deviation / CAPA / bridge services.

public sealed class CreateDocumentQualityEventHandler(DocumentQualityEventService s)
    : IRequestHandler<CreateDocumentQualityEventCommand, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(CreateDocumentQualityEventCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class OpenDocumentQualityEventHandler(DocumentQualityEventService s)
    : IRequestHandler<OpenDocumentQualityEventCommand, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(OpenDocumentQualityEventCommand r, CancellationToken ct) =>
        s.OpenAsync(r.Id, r.CorrelationId, ct);
}

public sealed class CloseDocumentQualityEventHandler(DocumentQualityEventService s)
    : IRequestHandler<CloseDocumentQualityEventCommand, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(CloseDocumentQualityEventCommand r, CancellationToken ct) =>
        s.CloseAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class CancelDocumentQualityEventHandler(DocumentQualityEventService s)
    : IRequestHandler<CancelDocumentQualityEventCommand, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(CancelDocumentQualityEventCommand r, CancellationToken ct) =>
        s.CancelAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class LinkQualityEventSourceHandler(DocumentQualityEventService s)
    : IRequestHandler<LinkQualityEventSourceCommand, Response<QualityEventSourceLinkModel>>
{
    public Task<Response<QualityEventSourceLinkModel>> Handle(LinkQualityEventSourceCommand r, CancellationToken ct) =>
        s.LinkSourceAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class GetDocumentQualityEventsHandler(DocumentQualityEventService s)
    : IRequestHandler<GetDocumentQualityEventsQuery, Response<IReadOnlyList<QualityEventModel>>>
{
    public Task<Response<IReadOnlyList<QualityEventModel>>> Handle(GetDocumentQualityEventsQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetDocumentQualityEventByIdHandler(DocumentQualityEventService s)
    : IRequestHandler<GetDocumentQualityEventByIdQuery, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(GetDocumentQualityEventByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetQualityEventSourceLinksHandler(DocumentQualityEventService s)
    : IRequestHandler<GetQualityEventSourceLinksQuery, Response<IReadOnlyList<QualityEventSourceLinkModel>>>
{
    public Task<Response<IReadOnlyList<QualityEventSourceLinkModel>>> Handle(GetQualityEventSourceLinksQuery r, CancellationToken ct) =>
        s.GetSourceLinksAsync(r.Id, r.CorrelationId, ct);
}

// ── deviations ───────────────────────────────────────────────────────────────

public sealed class CreateDocumentDeviationHandler(DocumentDeviationService s)
    : IRequestHandler<CreateDocumentDeviationCommand, Response<DeviationModel>>
{
    public Task<Response<DeviationModel>> Handle(CreateDocumentDeviationCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class OpenDocumentDeviationHandler(DocumentDeviationService s)
    : IRequestHandler<OpenDocumentDeviationCommand, Response<DeviationModel>>
{
    public Task<Response<DeviationModel>> Handle(OpenDocumentDeviationCommand r, CancellationToken ct) =>
        s.OpenInvestigationAsync(r.Id, r.CorrelationId, ct);
}

public sealed class RecordDeviationInvestigationHandler(DocumentDeviationService s)
    : IRequestHandler<RecordDeviationInvestigationCommand, Response<DeviationModel>>
{
    public Task<Response<DeviationModel>> Handle(RecordDeviationInvestigationCommand r, CancellationToken ct) =>
        s.RecordInvestigationAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class RequireCAPAForDeviationHandler(DocumentDeviationService s)
    : IRequestHandler<RequireCAPAForDeviationCommand, Response<DeviationModel>>
{
    public Task<Response<DeviationModel>> Handle(RequireCAPAForDeviationCommand r, CancellationToken ct) =>
        s.RequireCapaAsync(r.Id, r.CorrelationId, ct);
}

public sealed class CloseDocumentDeviationHandler(DocumentDeviationService s)
    : IRequestHandler<CloseDocumentDeviationCommand, Response<DeviationModel>>
{
    public Task<Response<DeviationModel>> Handle(CloseDocumentDeviationCommand r, CancellationToken ct) =>
        s.CloseAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class CancelDocumentDeviationHandler(DocumentDeviationService s)
    : IRequestHandler<CancelDocumentDeviationCommand, Response<DeviationModel>>
{
    public Task<Response<DeviationModel>> Handle(CancelDocumentDeviationCommand r, CancellationToken ct) =>
        s.CancelAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class GetDocumentDeviationsHandler(DocumentDeviationService s)
    : IRequestHandler<GetDocumentDeviationsQuery, Response<IReadOnlyList<DeviationModel>>>
{
    public Task<Response<IReadOnlyList<DeviationModel>>> Handle(GetDocumentDeviationsQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetDocumentDeviationByIdHandler(DocumentDeviationService s)
    : IRequestHandler<GetDocumentDeviationByIdQuery, Response<DeviationModel>>
{
    public Task<Response<DeviationModel>> Handle(GetDocumentDeviationByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

// ── CAPA actions ─────────────────────────────────────────────────────────────

public sealed class CreateDocumentCAPAActionHandler(DocumentCapaActionService s)
    : IRequestHandler<CreateDocumentCAPAActionCommand, Response<CapaActionModel>>
{
    public Task<Response<CapaActionModel>> Handle(CreateDocumentCAPAActionCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class StartCAPAActionHandler(DocumentCapaActionService s)
    : IRequestHandler<StartCAPAActionCommand, Response<CapaActionModel>>
{
    public Task<Response<CapaActionModel>> Handle(StartCAPAActionCommand r, CancellationToken ct) =>
        s.StartAsync(r.Id, r.CorrelationId, ct);
}

public sealed class CompleteCAPAActionHandler(DocumentCapaActionService s)
    : IRequestHandler<CompleteCAPAActionCommand, Response<CapaActionModel>>
{
    public Task<Response<CapaActionModel>> Handle(CompleteCAPAActionCommand r, CancellationToken ct) =>
        s.CompleteAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class RecordCAPAEffectivenessHandler(DocumentCapaActionService s)
    : IRequestHandler<RecordCAPAEffectivenessCommand, Response<CapaActionModel>>
{
    public Task<Response<CapaActionModel>> Handle(RecordCAPAEffectivenessCommand r, CancellationToken ct) =>
        s.RecordEffectivenessAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class CloseCAPAActionHandler(DocumentCapaActionService s)
    : IRequestHandler<CloseCAPAActionCommand, Response<CapaActionModel>>
{
    public Task<Response<CapaActionModel>> Handle(CloseCAPAActionCommand r, CancellationToken ct) =>
        s.CloseAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class CancelCAPAActionHandler(DocumentCapaActionService s)
    : IRequestHandler<CancelCAPAActionCommand, Response<CapaActionModel>>
{
    public Task<Response<CapaActionModel>> Handle(CancelCAPAActionCommand r, CancellationToken ct) =>
        s.CancelAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class GetDocumentCAPAActionsHandler(DocumentCapaActionService s)
    : IRequestHandler<GetDocumentCAPAActionsQuery, Response<IReadOnlyList<CapaActionModel>>>
{
    public Task<Response<IReadOnlyList<CapaActionModel>>> Handle(GetDocumentCAPAActionsQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetDocumentCAPAActionByIdHandler(DocumentCapaActionService s)
    : IRequestHandler<GetDocumentCAPAActionByIdQuery, Response<CapaActionModel>>
{
    public Task<Response<CapaActionModel>> Handle(GetDocumentCAPAActionByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

// ── bridge ───────────────────────────────────────────────────────────────────

public sealed class BridgeQualityEventFromSourceHandler(DocumentQualityEventBridgeService s)
    : IRequestHandler<BridgeQualityEventFromSourceCommand, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(BridgeQualityEventFromSourceCommand r, CancellationToken ct) =>
        s.FromSourceAsync(r.Input, r.CorrelationId, ct);
}

public sealed class BridgeQualityEventFromGDocPCorrectionHandler(DocumentQualityEventBridgeService s)
    : IRequestHandler<BridgeQualityEventFromGDocPCorrectionCommand, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(BridgeQualityEventFromGDocPCorrectionCommand r, CancellationToken ct) =>
        s.FromGDocPCorrectionAsync(r.CorrectionId, r.SeverityOverride, r.CorrelationId, ct);
}

public sealed class BridgeQualityEventFromObsoleteCopyFindingHandler(DocumentQualityEventBridgeService s)
    : IRequestHandler<BridgeQualityEventFromObsoleteCopyFindingCommand, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(BridgeQualityEventFromObsoleteCopyFindingCommand r, CancellationToken ct) =>
        s.FromObsoleteCopyFindingAsync(r.FindingId, r.SeverityOverride, r.CorrelationId, ct);
}

public sealed class BridgeQualityEventFromTemporaryIssueHandler(DocumentQualityEventBridgeService s)
    : IRequestHandler<BridgeQualityEventFromTemporaryIssueCommand, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(BridgeQualityEventFromTemporaryIssueCommand r, CancellationToken ct) =>
        s.FromTemporaryIssueAsync(r.IssueId, r.SeverityOverride, r.CorrelationId, ct);
}

public sealed class BridgeQualityEventFromExternalImpactHandler(DocumentQualityEventBridgeService s)
    : IRequestHandler<BridgeQualityEventFromExternalImpactCommand, Response<QualityEventModel>>
{
    public Task<Response<QualityEventModel>> Handle(BridgeQualityEventFromExternalImpactCommand r, CancellationToken ct) =>
        s.FromExternalImpactAssessmentAsync(r.AssessmentId, r.SeverityOverride, r.CorrelationId, ct);
}
