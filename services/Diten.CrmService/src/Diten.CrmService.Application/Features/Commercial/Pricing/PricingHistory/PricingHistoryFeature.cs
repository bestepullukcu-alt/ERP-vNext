using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Application.Features.Commercial.Pricing.Evaluation;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PricingHistory;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.7: the change history (who, when, old → new) and the dictionary of the module.

public sealed record PricingChangeDto(Guid Id, string EntityType, Guid EntityId, int EntityVersion, Guid? ParentId, string Action, string? ByUserId,
    DateTimeOffset At, IReadOnlyList<PricingFieldChange> Changes);

public sealed record PricingChangePageDto(IReadOnlyList<PricingChangeDto> Items, long Total, int Page, int PageSize);

/// <summary><paramref name="AllowedTypes"/> = the entity types the caller may read (the controller decides from the keys:
/// price lists + items, rules, rates); a requested type outside them reads nothing.</summary>
public sealed record GetPricingChangeLogQuery(
    IReadOnlyCollection<string> AllowedTypes, string? EntityType, Guid? EntityId, Guid? ParentId, string? ByUserId, DateTimeOffset? From,
    DateTimeOffset? To, int? Page, int? PageSize) : IRequest<Response<PricingChangePageDto>>;

public sealed record PricingReasonCodeDto(string Code, string Severity, IReadOnlyList<string> Params);

public sealed record PricingContractDto(
    IReadOnlyList<string> Kinds, IReadOnlyList<string> Tiers, string DefaultTier, IReadOnlyList<string> ListStatuses, IReadOnlyList<string> Phases,
    IReadOnlyList<string> RuleStatuses, IReadOnlyList<string> RuleChangeKinds, IReadOnlyList<string> ImportRowStatuses, IReadOnlyList<string> Outcomes,
    IReadOnlyList<PricingReasonCodeDto> ReasonCodes, IReadOnlyList<string> FxSources, bool SegmentsEnabled, IReadOnlyList<string> ChangeEntityTypes);

public sealed record GetPricingContractQuery : IRequest<Response<PricingContractDto>>;

public sealed class PricingHistoryHandlers :
    IRequestHandler<GetPricingChangeLogQuery, Response<PricingChangePageDto>>,
    IRequestHandler<GetPricingContractQuery, Response<PricingContractDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPricingChangeLogRepository _log;

    public PricingHistoryHandlers(ITenantContext tenant, IPricingChangeLogRepository log)
    {
        _tenant = tenant;
        _log = log;
    }

    public async Task<Response<PricingChangePageDto>> Handle(GetPricingChangeLogQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PricingChangePageDto>("Tenant context is required.");
        }

        var type = PricingCommon.Clean(request.EntityType)?.ToLowerInvariant();
        if (type is not null && !PricingCommon.In(type, PricingChangeEntityTypes.All))
        {
            return PricingCommon.Invalid<PricingChangePageDto>("entityType is price-list, price-list-item, pricing-rule or fx-rate.");
        }

        var types = type is null ? request.AllowedTypes.ToList() : request.AllowedTypes.Contains(type) ? new List<string> { type } : new List<string>();
        var (page, size) = PriceLists.Handlers.QueryHandlers.PriceListQueryHandlers.Paging(request.Page, request.PageSize);
        if (types.Count == 0)
        {
            return Response<PricingChangePageDto>.Success(new PricingChangePageDto(Array.Empty<PricingChangeDto>(), 0, page, size));
        }

        var (items, total) = await _log.ListAsync(tenantId, new PricingChangeLogFilter(types, request.EntityId, request.ParentId,
            PricingCommon.Clean(request.ByUserId), request.From, request.To), (page - 1) * size, size, ct);
        return Response<PricingChangePageDto>.Success(new PricingChangePageDto(items.Where(e => e.TenantId == tenantId)
            .Select(e => new PricingChangeDto(e.Id, e.EntityType, e.EntityId, e.EntityVersion, e.ParentId, e.Action, e.ByUserId, e.At, e.Changes)).ToList(),
            total, page, size));
    }

    public Task<Response<PricingContractDto>> Handle(GetPricingContractQuery request, CancellationToken ct)
        => Task.FromResult(Response<PricingContractDto>.Success(new PricingContractDto(
            PriceListKinds.All, PriceTiers.All, PriceTiers.PharmacistPurchase, PriceListStatuses.All, PriceListPhases.All, PricingRuleStatuses.All,
            PricingRuleChangeKinds.All, PriceListImportRowStatuses.All, PricingOutcomes.All,
            new[]
            {
                Code(PricingReasonCodes.DiscountSoftExceeded, PricingFindingSeverities.Soft, "value", "limit"),
                Code(PricingReasonCodes.DiscountHardExceeded, PricingFindingSeverities.Hard, "value", "limit"),
                Code(PricingReasonCodes.MfSoftExceeded, PricingFindingSeverities.Soft, "value", "valuePct", "limit", "limitPct"),
                Code(PricingReasonCodes.MfHardExceeded, PricingFindingSeverities.Hard, "value", "valuePct", "limit", "limitPct"),
                Code(PricingReasonCodes.CombinedSoftExceeded, PricingFindingSeverities.Soft, "valuePct", "limitPct"),
                Code(PricingReasonCodes.CombinedHardExceeded, PricingFindingSeverities.Hard, "valuePct", "limitPct"),
                Code(PricingReasonCodes.QuantityBelowMin, PricingFindingSeverities.Hard, "value", "min"),
                Code(PricingReasonCodes.QuantitySoftExceeded, PricingFindingSeverities.Soft, "value", "limit"),
                Code(PricingReasonCodes.QuantityHardExceeded, PricingFindingSeverities.Hard, "value", "limit"),
                Code(PricingReasonCodes.SkuRetired, PricingFindingSeverities.Hard, "skuId"),
                Code(PricingReasonCodes.PriceSkuNotInList, PricingFindingSeverities.Hard, "skuId", "priceListName"),
                Code(PricingReasonCodes.DiscountNotAllowedQuantityMode, PricingFindingSeverities.Hard)
            },
            new[] { "manual", "import" }, RuleSpecificity.SegmentsEnabled, PricingChangeEntityTypes.All)));

    private static PricingReasonCodeDto Code(string code, string severity, params string[] parameters) => new(code, severity, parameters);
}
