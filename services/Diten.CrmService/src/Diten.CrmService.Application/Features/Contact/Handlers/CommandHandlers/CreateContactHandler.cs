using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainContact = Diten.CrmService.Domain.Entities.Contact;
using DomainExternalRef = Diten.CrmService.Domain.Entities.ContactExternalReference;

namespace Diten.CrmService.Application.Features.Contact.Handlers.CommandHandlers;

public sealed class CreateContactHandler : IRequestHandler<CreateContactCommand, Response<Guid>>
{
    private const string DefaultSourceSystem = "default";
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IContactExternalReferenceRepository _externalRefs;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public CreateContactHandler(
        ITenantContext tenant,
        IContactRepository contacts,
        IContactExternalReferenceRepository externalRefs,
        IReferenceDataValidator referenceValidator,
        IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _contacts = contacts;
        _externalRefs = externalRefs;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var referenceErrors = await ContactReferenceValidation.ValidateAsync(
            _referenceValidator, request.ContactType, request.Status,
            request.CountryRef, request.CityRef, request.DistrictRef,
            request.ProfessionalTitle, request.Specialty, request.Department, request.Gender, cancellationToken);
        if (referenceErrors.Count != 0)
        {
            return Response<Guid>.Fail(referenceErrors, 400);
        }

        // Pre-validate external reference uniqueness before any insert to avoid partial state.
        string? sourceSystem = null;
        if (request.ExternalReference is { } externalInput && !string.IsNullOrWhiteSpace(externalInput.ExternalId))
        {
            sourceSystem = string.IsNullOrWhiteSpace(externalInput.SourceSystem)
                ? DefaultSourceSystem
                : externalInput.SourceSystem!.Trim();

            if (await _externalRefs.ExistsBySourceExternalAsync(
                    tenantId, sourceSystem, externalInput.ExternalId.Trim(), excludeId: null, cancellationToken))
            {
                // PII-safe: audit the conflict by SourceSystem (a non-PII code), never the raw external id value.
                await _audit.PublishAsync(ContactAuditEvents.DuplicateRejected, tenantId, Guid.Empty, $"source={sourceSystem}", cancellationToken);
                return Response<Guid>.Fail("An external reference with this SourceSystem + ExternalId already exists.", 409);
            }
        }

        var contact = new DomainContact
        {
            TenantId = tenantId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName?.Trim() ?? string.Empty,
            DisplayName = ContactMapper.ResolveDisplayName(request.DisplayName, request.FirstName, request.LastName),
            ContactType = request.ContactType.Trim(),
            Status = request.Status.Trim(),
            Gender = request.Gender?.Trim(),
            PhotoDataUri = string.IsNullOrWhiteSpace(request.PhotoDataUri) ? null : request.PhotoDataUri.Trim(),
            ProfessionalTitle = request.ProfessionalTitle?.Trim(),
            Specialty = request.Specialty?.Trim(),
            Department = request.Department?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim().ToLowerInvariant(),
            Notes = request.Notes?.Trim(),
            CountryRef = request.CountryRef?.Trim(),
            CityRef = request.CityRef?.Trim(),
            DistrictRef = request.DistrictRef?.Trim(),
            AddressLine = request.AddressLine?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            PreferredLanguage = request.PreferredLanguage?.Trim(),
            PhoneCountryCode = request.PhoneCountryCode?.Trim()
        };

        await _contacts.InsertAsync(contact, cancellationToken);

        if (sourceSystem is not null && request.ExternalReference is { } input)
        {
            await _externalRefs.InsertAsync(new DomainExternalRef
            {
                TenantId = tenantId,
                ContactId = contact.Id,
                SourceSystem = sourceSystem,
                ExternalId = input.ExternalId.Trim(),
                SourceEntity = input.SourceEntity?.Trim(),
                DisplayName = input.DisplayName?.Trim()
            }, cancellationToken);
        }

        // PII-safe: identify the record by ContactId only; the DisplayName (name = PII) is never written to audit/log.
        await _audit.PublishAsync(ContactAuditEvents.Create, tenantId, contact.Id, detail: null, cancellationToken);
        return Response<Guid>.Success(contact.Id, 201);
    }
}
