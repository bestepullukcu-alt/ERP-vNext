using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;

/// <summary>
/// MOD-0149 §30 U-1: does the account exist in the caller's tenant, what is its status, is it billable. Missing,
/// soft-deleted and other-tenant accounts are one 404 with one body (the repository read is tenant + <c>!IsDeleted</c>,
/// so the three cannot be told apart here). A store failure is 503, never 404 / 200 / 500 (contract §7).
/// </summary>
public sealed class GetAccountLookupValidationHandler
    : IRequestHandler<GetAccountLookupValidationQuery, Response<AccountLookupValidationDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;

    public GetAccountLookupValidationHandler(ITenantContext tenant, IAccountRepository accounts)
    {
        _tenant = tenant;
        _accounts = accounts;
    }

    public async Task<Response<AccountLookupValidationDto>> Handle(
        GetAccountLookupValidationQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountLookupValidationDto>.Fail(AccountLookupContract.TenantRequiredMessage, 400);
        }

        Domain.Entities.Account? account;
        try
        {
            account = await _accounts.GetByIdAsync(tenantId, request.AccountId, cancellationToken);
        }
        catch (Exception ex) when (AccountLookupContract.IsStoreUnavailable(ex))
        {
            return Response<AccountLookupValidationDto>.Fail(AccountLookupContract.StoreUnavailableMessage, 503);
        }

        return account is null
            ? Response<AccountLookupValidationDto>.Fail(AccountLookupContract.NotFoundMessage, 404)
            : Response<AccountLookupValidationDto>.Success(AccountLookupContract.ToValidation(account));
    }
}
