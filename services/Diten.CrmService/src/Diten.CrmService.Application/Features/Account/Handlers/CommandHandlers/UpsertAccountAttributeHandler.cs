using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainAttribute = Diten.CrmService.Domain.Entities.AccountAttributeValue;

namespace Diten.CrmService.Application.Features.Account.Handlers.CommandHandlers;

public sealed class UpsertAccountAttributeHandler : IRequestHandler<UpsertAccountAttributeCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountAttributeValueRepository _attributes;
    private readonly IAccountAuditPublisher _audit;

    public UpsertAccountAttributeHandler(
        ITenantContext tenant,
        IAccountRepository accounts,
        IAccountAttributeValueRepository attributes,
        IAccountAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _attributes = attributes;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpsertAccountAttributeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.AttributeCode))
        {
            return Response<bool>.Fail("AttributeCode is required.", 400);
        }

        var account = await _accounts.GetByIdAsync(tenantId, request.AccountId, cancellationToken);
        if (account is null)
        {
            return Response<bool>.Fail("Account not found.", 404);
        }

        await _attributes.UpsertAsync(new DomainAttribute
        {
            TenantId = tenantId,
            AccountId = request.AccountId,
            AttributeCode = request.AttributeCode.Trim(),
            Value = request.Value
        }, cancellationToken);

        await _audit.PublishAsync(AccountAuditEvents.AttributeUpdate, tenantId, request.AccountId, request.AttributeCode.Trim(), cancellationToken);
        return Response<bool>.Success(true);
    }
}
