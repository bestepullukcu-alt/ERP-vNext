using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Queries;

/// <summary><paramref name="Status"/> = a stored status or a phase badge (planned / in-force / past).</summary>
public sealed record GetPriceListListQuery(
    Guid? LegalEntityId, string? CountryCode, string? Currency, string? Kind, string? Status, string? Search, int? Page, int? PageSize)
    : IRequest<Response<PriceListPageDto>>;

public sealed record GetPriceListByIdQuery(Guid Id) : IRequest<Response<PriceListDto>>;

/// <summary><paramref name="Diff"/> = <c>previous</c>: each item carries its difference against the previous version, and the
/// page lists the products the previous version had and this one does not.</summary>
public sealed record GetPriceListItemsQuery(Guid Id, string? Search, string? Diff, int? Page, int? PageSize)
    : IRequest<Response<PriceListItemPageDto>>;

/// <summary>Excel of one list's items (<paramref name="PriceListId"/>) or of the list page (filter).</summary>
public sealed record GetPriceListExportQuery(Guid? PriceListId, Guid? LegalEntityId, string? CountryCode, string? Currency, string? Kind,
    string? Status) : IRequest<Response<PriceListExportDto>>;
