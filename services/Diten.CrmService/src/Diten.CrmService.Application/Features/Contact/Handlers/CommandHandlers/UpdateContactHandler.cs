using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Contact.Handlers.CommandHandlers;

public sealed class UpdateContactHandler : IRequestHandler<UpdateContactCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public UpdateContactHandler(
        ITenantContext tenant,
        IContactRepository contacts,
        IReferenceDataValidator referenceValidator,
        IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _contacts = contacts;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
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

        var referenceErrors = await ContactReferenceValidation.ValidateAsync(
            _referenceValidator, request.ContactType, request.Status,
            request.CountryRef, request.CityRef, request.DistrictRef,
            request.ProfessionalTitle, request.Specialty, request.Department, request.Gender, cancellationToken);
        if (referenceErrors.Count != 0)
        {
            return Response<bool>.Fail(referenceErrors, 400);
        }

        contact.FirstName = request.FirstName.Trim();
        contact.LastName = request.LastName?.Trim() ?? string.Empty;
        contact.DisplayName = ContactMapper.ResolveDisplayName(request.DisplayName, request.FirstName, request.LastName);
        contact.ContactType = request.ContactType.Trim();
        contact.Status = request.Status.Trim();
        contact.Gender = request.Gender?.Trim();
        contact.PhotoDataUri = string.IsNullOrWhiteSpace(request.PhotoDataUri) ? null : request.PhotoDataUri.Trim();
        contact.ProfessionalTitle = request.ProfessionalTitle?.Trim();
        contact.Specialty = request.Specialty?.Trim();
        contact.Department = request.Department?.Trim();
        contact.Phone = request.Phone?.Trim();
        contact.Email = request.Email?.Trim().ToLowerInvariant();
        contact.Notes = request.Notes?.Trim();
        contact.CountryRef = request.CountryRef?.Trim();
        contact.CityRef = request.CityRef?.Trim();
        contact.DistrictRef = request.DistrictRef?.Trim();
        contact.AddressLine = request.AddressLine?.Trim();
        contact.PostalCode = request.PostalCode?.Trim();
        contact.PreferredLanguage = request.PreferredLanguage?.Trim();
        contact.PhoneCountryCode = request.PhoneCountryCode?.Trim();
        contact.UpdatedAt = DateTimeOffset.UtcNow;

        await _contacts.UpdateAsync(contact, cancellationToken);
        // PII-safe: identify by ContactId only; never write the DisplayName (name = PII) to audit/log.
        await _audit.PublishAsync(ContactAuditEvents.Update, tenantId, contact.Id, detail: null, cancellationToken);
        return Response<bool>.Success(true);
    }
}
