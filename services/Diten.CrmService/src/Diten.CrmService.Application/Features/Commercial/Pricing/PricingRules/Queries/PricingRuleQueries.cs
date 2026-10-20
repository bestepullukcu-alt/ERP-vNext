using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Queries;

public sealed record GetPricingRuleListQuery(Guid? LegalEntityId, Guid? SkuId, string? SegmentCode, string? Status, string? Search, int? Page, int? PageSize)
    : IRequest<Response<PricingRulePageDto>>;

public sealed record GetPricingRuleByIdQuery(Guid Id) : IRequest<Response<PricingRuleDetailDto>>;

/// <summary>Kural deneme — a READ (it saves nothing); the evaluation of the given lines on the published data.</summary>
public sealed record EvaluatePricingTrialQuery(PricingTrialInput Input) : IRequest<Response<EvaluationResult>>;
