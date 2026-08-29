using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.CommandHandlers;

public sealed class LinkParentAccountHandler : IRequestHandler<LinkParentAccountCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountAuditPublisher _audit;

    public LinkParentAccountHandler(ITenantContext tenant, IAccountRepository accounts, IAccountAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(LinkParentAccountCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        if (request.AccountId == request.ParentAccountId)
        {
            return Response<bool>.Fail("An account cannot be its own parent.", 400);
        }

        var account = await _accounts.GetByIdAsync(tenantId, request.AccountId, cancellationToken);
        if (account is null)
        {
            return Response<bool>.Fail("Account not found.", 404);
        }

        var parent = await _accounts.GetByIdAsync(tenantId, request.ParentAccountId, cancellationToken);
        if (parent is null)
        {
            return Response<bool>.Fail("Parent account not found.", 404);
        }

        if (await _accounts.WouldCreateCycleAsync(tenantId, request.AccountId, request.ParentAccountId, cancellationToken))
        {
            return Response<bool>.Fail("Linking this parent would create a circular hierarchy.", 400);
        }

        account.ParentAccountId = request.ParentAccountId;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await _accounts.UpdateAsync(account, cancellationToken);

        await _audit.PublishAsync(AccountAuditEvents.HierarchyLink, tenantId, account.Id, request.ParentAccountId.ToString(), cancellationToken);
        return Response<bool>.Success(true);
    }
}
