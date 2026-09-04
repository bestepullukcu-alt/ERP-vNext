using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Queries;

public sealed record GetAccountListQuery(
    string? Search, int Page = 1, int PageSize = 25, string? SortBy = null, string? SortDir = null,
    string? Status = null, string? AccountType = null,
    // MOD-0151 territory-coverage chips. Both arrive as comma-separated multi-select values (matching Status /
    // AccountType): TerritoryNodeId carries current-coverage node ids, CountryScope carries owning-model `country`
    // scope codes. They are resolved to a current-coverage account-id set (both lifecycle gates at now) and ANDed
    // onto the account query — they are NOT stored Account fields.
    string? TerritoryNodeId = null, string? CountryScope = null)
    : IRequest<Response<PagedResult<AccountListItemDto>>>;
