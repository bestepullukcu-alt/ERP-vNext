using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;

public sealed class GetAccountByIdHandler : IRequestHandler<GetAccountByIdQuery, Response<AccountDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountExternalReferenceRepository _externalRefs;
    private readonly IAccountAttributeValueRepository _attributes;

    public GetAccountByIdHandler(
        ITenantContext tenant,
        IAccountRepository accounts,
        IAccountExternalReferenceRepository externalRefs,
        IAccountAttributeValueRepository attributes)
    {
        _tenant = tenant;
        _accounts = accounts;
        _externalRefs = externalRefs;
        _attributes = attributes;
    }

    public async Task<Response<AccountDetailDto>> Handle(GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountDetailDto>.Fail("Tenant context is required.", 400);
        }

        var account = await _accounts.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (account is null)
        {
            return Response<AccountDetailDto>.Fail("Account not found.", 404);
        }

        var externalRefs = (await _externalRefs.ListByAccountAsync(tenantId, account.Id, cancellationToken))
            .Select(AccountMapper.ToDto).ToList();
        var attributes = (await _attributes.ListByAccountAsync(tenantId, account.Id, cancellationToken))
            .Select(AccountMapper.ToDto).ToList();

        return Response<AccountDetailDto>.Success(AccountMapper.ToDetail(account, externalRefs, attributes));
    }
}
