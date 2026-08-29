using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;

public sealed class GetAccountOverviewHandler : IRequestHandler<GetAccountOverviewQuery, Response<AccountOverviewDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountExternalReferenceRepository _externalRefs;
    private readonly IAccountAttributeValueRepository _attributes;

    public GetAccountOverviewHandler(
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

    public async Task<Response<AccountOverviewDto>> Handle(GetAccountOverviewQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountOverviewDto>.Fail("Tenant context is required.", 400);
        }

        var account = await _accounts.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (account is null)
        {
            return Response<AccountOverviewDto>.Fail("Account not found.", 404);
        }

        var externalRefs = (await _externalRefs.ListByAccountAsync(tenantId, account.Id, cancellationToken))
            .Select(AccountMapper.ToDto).ToList();
        var attributes = (await _attributes.ListByAccountAsync(tenantId, account.Id, cancellationToken))
            .Select(AccountMapper.ToDto).ToList();
        var children = (await _accounts.GetChildrenAsync(tenantId, account.Id, cancellationToken))
            .Select(AccountMapper.ToListItem).ToList();

        var detail = AccountMapper.ToDetail(account, externalRefs, attributes);
        // Coverage is a read-only projection owned by MOD-0151; not available until MOD-0151 ships (§3.1).
        var overview = new AccountOverviewDto(detail, account.ParentAccountId, children, CoverageSummaryDto.NotAvailable());
        return Response<AccountOverviewDto>.Success(overview);
    }
}
