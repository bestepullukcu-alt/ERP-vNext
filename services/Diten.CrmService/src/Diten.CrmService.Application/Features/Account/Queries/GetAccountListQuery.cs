using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Queries;

public sealed record GetAccountListQuery(string? Search, int Page = 1, int PageSize = 25)
    : IRequest<Response<PagedResult<AccountListItemDto>>>;
