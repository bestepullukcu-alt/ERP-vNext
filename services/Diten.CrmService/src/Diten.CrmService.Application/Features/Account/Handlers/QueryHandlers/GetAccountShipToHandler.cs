using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;

/// <summary>
/// MOD-0149 §32 U-4: the ship-to copy of one account of the caller's tenant (identity, code, name, status, delivery
/// addresses). Read-only. Missing, soft-deleted and other-tenant accounts are one 404 with U-1's body (the repository
/// read is tenant + <c>!IsDeleted</c>). The status is NOT part of the 404 decision: a non-active account answers 200
/// with its status as stored (the consumer decides, K-14). A store failure is 503, never 404 / 200 / 500.
/// </summary>
public sealed class GetAccountShipToHandler : IRequestHandler<GetAccountShipToQuery, Response<AccountShipToDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;

    public GetAccountShipToHandler(ITenantContext tenant, IAccountRepository accounts)
    {
        _tenant = tenant;
        _accounts = accounts;
    }

    public async Task<Response<AccountShipToDto>> Handle(GetAccountShipToQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountShipToDto>.Fail(AccountLookupContract.TenantRequiredMessage, 400);
        }

        Domain.Entities.Account? account;
        try
        {
            account = await _accounts.GetByIdAsync(tenantId, request.AccountId, cancellationToken);
        }
        catch (Exception ex) when (AccountLookupContract.IsStoreUnavailable(ex))
        {
            return Response<AccountShipToDto>.Fail(AccountLookupContract.StoreUnavailableMessage, 503);
        }

        return account is null
            ? Response<AccountShipToDto>.Fail(AccountLookupContract.NotFoundMessage, 404)
            : Response<AccountShipToDto>.Success(AccountLookupContract.ToShipTo(account));
    }
}
