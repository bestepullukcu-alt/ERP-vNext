using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Commands;
using Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Queries;
using Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Handlers;

// MOD-0029-FU18 — thin MediatR handlers delegating to the variant localization service and the parent change evaluator.

public sealed class UpsertVariantLocalizationProfileHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<UpsertVariantLocalizationProfileCommand, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(UpsertVariantLocalizationProfileCommand r, CancellationToken ct) =>
        s.UpsertProfileAsync(r.VariantId, r.Input, r.CorrelationId, ct);
}

public sealed class RequireBilingualReviewHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<RequireBilingualReviewCommand, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(RequireBilingualReviewCommand r, CancellationToken ct) =>
        s.RequireBilingualReviewAsync(r.VariantId, r.CorrelationId, ct);
}

public sealed class RecordBilingualReviewEvidenceHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<RecordBilingualReviewEvidenceCommand, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(RecordBilingualReviewEvidenceCommand r, CancellationToken ct) =>
        s.RecordBilingualReviewAsync(r.VariantId, r.Input, r.CorrelationId, ct);
}

public sealed class RejectBilingualReviewHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<RejectBilingualReviewCommand, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(RejectBilingualReviewCommand r, CancellationToken ct) =>
        s.RejectBilingualReviewAsync(r.VariantId, r.Input, r.CorrelationId, ct);
}

public sealed class RequireLocalApprovalHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<RequireLocalApprovalCommand, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(RequireLocalApprovalCommand r, CancellationToken ct) =>
        s.RequireLocalApprovalAsync(r.VariantId, r.CorrelationId, ct);
}

public sealed class RecordLocalApprovalEvidenceHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<RecordLocalApprovalEvidenceCommand, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(RecordLocalApprovalEvidenceCommand r, CancellationToken ct) =>
        s.RecordLocalApprovalAsync(r.VariantId, r.Input, r.CorrelationId, ct);
}

public sealed class RejectLocalApprovalHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<RejectLocalApprovalCommand, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(RejectLocalApprovalCommand r, CancellationToken ct) =>
        s.RejectLocalApprovalAsync(r.VariantId, r.Input, r.CorrelationId, ct);
}

public sealed class AllowTemporaryEnglishMasterHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<AllowTemporaryEnglishMasterCommand, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(AllowTemporaryEnglishMasterCommand r, CancellationToken ct) =>
        s.AllowTemporaryEnglishMasterAsync(r.VariantId, r.Input, r.CorrelationId, ct);
}

public sealed class RevokeTemporaryEnglishMasterHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<RevokeTemporaryEnglishMasterCommand, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(RevokeTemporaryEnglishMasterCommand r, CancellationToken ct) =>
        s.RevokeTemporaryEnglishMasterAsync(r.VariantId, r.CorrelationId, ct);
}

public sealed class EvaluateVariantParentChangeHandler(TemplateVariantParentChangeEvaluator s)
    : IRequestHandler<EvaluateVariantParentChangeCommand, Response<VariantParentChangeAssessmentModel>>
{
    public Task<Response<VariantParentChangeAssessmentModel>> Handle(EvaluateVariantParentChangeCommand r, CancellationToken ct) =>
        s.EvaluateAsync(r.VariantId, r.EvidenceReference, r.CorrelationId, ct);
}

public sealed class GetVariantLocalizationProfileHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<GetVariantLocalizationProfileQuery, Response<VariantLocalizationProfileModel>>
{
    public Task<Response<VariantLocalizationProfileModel>> Handle(GetVariantLocalizationProfileQuery r, CancellationToken ct) =>
        s.GetProfileAsync(r.VariantId, r.CorrelationId, ct);
}

public sealed class GetVariantReadinessHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<GetVariantReadinessQuery, Response<VariantReadinessModel>>
{
    public Task<Response<VariantReadinessModel>> Handle(GetVariantReadinessQuery r, CancellationToken ct) =>
        s.GetReadinessAsync(r.VariantId, r.CorrelationId, ct);
}

public sealed class GetVariantReviewEvidenceHandler(TemplateVariantLocalizationService s)
    : IRequestHandler<GetVariantReviewEvidenceQuery, Response<IReadOnlyList<VariantReviewEvidenceModel>>>
{
    public Task<Response<IReadOnlyList<VariantReviewEvidenceModel>>> Handle(GetVariantReviewEvidenceQuery r, CancellationToken ct) =>
        s.GetEvidenceAsync(r.VariantId, r.CorrelationId, ct);
}

public sealed class GetVariantParentChangeAssessmentsHandler(TemplateVariantParentChangeEvaluator s)
    : IRequestHandler<GetVariantParentChangeAssessmentsQuery, Response<IReadOnlyList<VariantParentChangeAssessmentModel>>>
{
    public Task<Response<IReadOnlyList<VariantParentChangeAssessmentModel>>> Handle(GetVariantParentChangeAssessmentsQuery r, CancellationToken ct) =>
        s.GetAssessmentsAsync(r.VariantId, r.CorrelationId, ct);
}
