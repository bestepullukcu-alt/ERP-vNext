using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;

/// <summary>
/// MOD-0149 §30 U-3: the Treasury AR customer picker. Input outside the contract bounds is 400 and nothing is read.
/// The search is literal text in the repository (escaped, Turkish-i insensitive), sorted name ascending then id
/// ascending on this path only. No match is 200 with an empty page; a store failure is 503, never an empty list.
/// </summary>
public sealed class SearchAccountLookupHandler : IRequestHandler<SearchAccountLookupQuery, Response<AccountLookupPageDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;

    public SearchAccountLookupHandler(ITenantContext tenant, IAccountRepository accounts)
    {
        _tenant = tenant;
        _accounts = accounts;
    }

    public async Task<Response<AccountLookupPageDto>> Handle(SearchAccountLookupQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountLookupPageDto>.Fail(AccountLookupContract.TenantRequiredMessage, 400);
        }

        var search = request.Search?.Trim() ?? string.Empty;
        if (search.Length < AccountLookupContract.SearchMinLength || search.Length > AccountLookupContract.SearchMaxLength)
        {
            return Response<AccountLookupPageDto>.Fail(
                $"search must be {AccountLookupContract.SearchMinLength} to {AccountLookupContract.SearchMaxLength} characters after trimming.",
                400);
        }

        var page = request.Page ?? 1;
        if (page < 1)
        {
            return Response<AccountLookupPageDto>.Fail("page must be 1 or greater.", 400);
        }

        var pageSize = request.PageSize ?? AccountLookupContract.DefaultPageSize;
        if (pageSize < 1 || pageSize > AccountLookupContract.MaxPageSize)
        {
            return Response<AccountLookupPageDto>.Fail(
                $"pageSize must be between 1 and {AccountLookupContract.MaxPageSize}.", 400);
        }

        var status = request.BillableOnly == true ? AccountLookupContract.BillableStatus : null;

        IReadOnlyList<Domain.Entities.Account> items;
        long total;
        try
        {
            (items, total) = await _accounts.SearchLookupAsync(tenantId, search, status, page, pageSize, cancellationToken);
        }
        catch (Exception ex) when (AccountLookupContract.IsStoreUnavailable(ex))
        {
            return Response<AccountLookupPageDto>.Fail(AccountLookupContract.StoreUnavailableMessage, 503);
        }

        return Response<AccountLookupPageDto>.Success(new AccountLookupPageDto(
            items.Select(AccountLookupContract.ToItem).ToList(), page, pageSize, total, AccountLookupContract.ContractVersion));
    }
}
