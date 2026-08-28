using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Queries;

// MOD-0029-FU18 — variant localization read queries (tenant-scoped; no side effects).

public sealed record GetVariantLocalizationProfileQuery(Guid VariantId, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>;

public sealed record GetVariantReadinessQuery(Guid VariantId, string CorrelationId)
    : IRequest<Response<VariantReadinessModel>>;

public sealed record GetVariantReviewEvidenceQuery(Guid VariantId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<VariantReviewEvidenceModel>>>;

public sealed record GetVariantParentChangeAssessmentsQuery(Guid VariantId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<VariantParentChangeAssessmentModel>>>;
