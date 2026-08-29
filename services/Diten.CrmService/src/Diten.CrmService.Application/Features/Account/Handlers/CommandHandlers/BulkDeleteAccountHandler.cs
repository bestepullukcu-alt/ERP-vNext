using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.CommandHandlers;

public sealed class BulkDeleteAccountHandler : IRequestHandler<BulkDeleteAccountCommand, Response<int>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountAuditPublisher _audit;

    public BulkDeleteAccountHandler(ITenantContext tenant, IAccountRepository accounts, IAccountAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _audit = audit;
    }

    public async Task<Response<int>> Handle(BulkDeleteAccountCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<int>.Fail("Tenant context is required.", 400);
        }

        var deleted = 0;
        foreach (var id in request.Ids.Distinct())
        {
            var account = await _accounts.GetByIdAsync(tenantId, id, cancellationToken);
            if (account is null)
            {
                continue;
            }

            account.IsDeleted = true;
            account.DeletedAt = DateTimeOffset.UtcNow;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _accounts.UpdateAsync(account, cancellationToken);
            await _audit.PublishAsync(AccountAuditEvents.Delete, tenantId, account.Id, account.AccountCode, cancellationToken);
            deleted++;
        }

        return Response<int>.Success(deleted);
    }
}
