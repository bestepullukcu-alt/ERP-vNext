using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.CommandHandlers;

public sealed class DeleteAccountHandler : IRequestHandler<DeleteAccountCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountAuditPublisher _audit;

    public DeleteAccountHandler(ITenantContext tenant, IAccountRepository accounts, IAccountAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var account = await _accounts.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (account is null)
        {
            return Response<bool>.Fail("Account not found.", 404);
        }

        account.IsDeleted = true;
        account.DeletedAt = DateTimeOffset.UtcNow;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await _accounts.UpdateAsync(account, cancellationToken);

        await _audit.PublishAsync(AccountAuditEvents.Delete, tenantId, account.Id, account.AccountCode, cancellationToken);
        return Response<bool>.Success(true);
    }
}
