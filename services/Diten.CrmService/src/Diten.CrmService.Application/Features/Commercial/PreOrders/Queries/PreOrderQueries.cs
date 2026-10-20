using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders.Queries;

// WP-VW-W6 W6-BE-a — MOD-0168 §3 reads. Every one is scoped to the tenant and to what the caller sees (§4.8).

/// <summary>The B list (server mode). <paramref name="AwaitingMe"/> = "waiting for my approval".</summary>
public sealed record GetPreOrderListQuery(
    string? Status, DateOnly? From, DateOnly? To, Guid? RepUserId, Guid? PharmacyAccountId, Guid? DepotAccountId, Guid? SkuId,
    Guid? LegalEntityId, string? CountryCode, bool AwaitingMe, string? Search, int? Page, int? PageSize) : IRequest<Response<PreOrderPageDto>>;

public sealed record GetPreOrderByIdQuery(Guid Id) : IRequest<Response<PreOrderDto>>;

/// <summary>Screen A's strip (seller, country, currencies, price mode) + the approver preview.</summary>
public sealed record GetPreOrderContextQuery : IRequest<Response<PreOrderContextDto>>;

/// <summary>The same MOD-0156 evaluation as the submission, nothing written.</summary>
public sealed record PreviewPreOrderQuery(PreOrderPreviewInput Input) : IRequest<Response<PreOrderPreviewDto>>;

/// <summary>Screen C: the pharmacy's orders of the last six months (the approver of the order, or read-all).</summary>
public sealed record GetPharmacyOrderHistoryQuery(Guid PreOrderId) : IRequest<Response<PreOrderPharmacyHistoryDto>>;

public sealed record GetPreOrderSettingsQuery : IRequest<Response<PreOrderSettingsDto>>;

/// <summary>The link screen after sign-in: the state, and the order only while the link is valid and the caller is its
/// approver.</summary>
public sealed record ResolvePreOrderApprovalLinkQuery(string Token) : IRequest<Response<PreOrderApprovalLinkDto>>;

public sealed record GetPreOrderExportQuery(GetPreOrderListQuery Filter) : IRequest<Response<PreOrderExportDto>>;
