using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Contact.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Contact.Handlers.CommandHandlers;

public sealed class DeleteContactHandler : IRequestHandler<DeleteContactCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IContactAuditPublisher _audit;

    public DeleteContactHandler(ITenantContext tenant, IContactRepository contacts, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _contacts = contacts;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(DeleteContactCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var contact = await _contacts.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (contact is null)
        {
            return Response<bool>.Fail("Contact not found.", 404);
        }

        contact.IsDeleted = true;
        contact.DeletedAt = DateTimeOffset.UtcNow;
        contact.UpdatedAt = DateTimeOffset.UtcNow;
        await _contacts.UpdateAsync(contact, cancellationToken);

        // PII-safe: identify by ContactId only; never write the DisplayName (name = PII) to audit/log.
        await _audit.PublishAsync(ContactAuditEvents.Delete, tenantId, contact.Id, detail: null, cancellationToken);
        return Response<bool>.Success(true);
    }
}
