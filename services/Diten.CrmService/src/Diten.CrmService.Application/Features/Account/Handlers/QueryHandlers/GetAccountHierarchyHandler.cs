using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainAccount = Diten.CrmService.Domain.Entities.Account;

namespace Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;

public sealed class GetAccountHierarchyHandler : IRequestHandler<GetAccountHierarchyQuery, Response<AccountHierarchyNodeDto>>
{
    private const int MaxDepth = 20;
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;

    public GetAccountHierarchyHandler(ITenantContext tenant, IAccountRepository accounts)
    {
        _tenant = tenant;
        _accounts = accounts;
    }

    public async Task<Response<AccountHierarchyNodeDto>> Handle(GetAccountHierarchyQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountHierarchyNodeDto>.Fail("Tenant context is required.", 400);
        }

        var root = await _accounts.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (root is null)
        {
            return Response<AccountHierarchyNodeDto>.Fail("Account not found.", 404);
        }

        var node = await BuildNodeAsync(tenantId, root, depth: 0, cancellationToken);
        return Response<AccountHierarchyNodeDto>.Success(node);
    }

    private async Task<AccountHierarchyNodeDto> BuildNodeAsync(Guid tenantId, DomainAccount account, int depth, CancellationToken cancellationToken)
    {
        var children = new List<AccountHierarchyNodeDto>();
        if (depth < MaxDepth)
        {
            foreach (var child in await _accounts.GetChildrenAsync(tenantId, account.Id, cancellationToken))
            {
                children.Add(await BuildNodeAsync(tenantId, child, depth + 1, cancellationToken));
            }
        }

        return new AccountHierarchyNodeDto(account.Id, account.AccountName, account.AccountCode, children);
    }
}
