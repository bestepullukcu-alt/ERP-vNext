using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainContact = Diten.CrmService.Domain.Entities.Contact;
using DomainContactRef = Diten.CrmService.Domain.Entities.ContactExternalReference;

namespace Diten.CrmService.Application.Features.ImportExport.Handlers;

/// <summary>Caches (setCode,value) reference validation so a bulk import hits the Gateway once per distinct value.</summary>
internal sealed class ReferenceCache
{
    private readonly IReferenceDataValidator _validator;
    private readonly Dictionary<(string, string), ReferenceValidationStatus> _cache = new();

    public ReferenceCache(IReferenceDataValidator validator) => _validator = validator;

    public async Task<ReferenceValidationStatus> StatusAsync(string setCode, string value, CancellationToken ct)
    {
        var key = (setCode, value.ToLowerInvariant());
        if (_cache.TryGetValue(key, out var status))
        {
            return status;
        }

        status = (await _validator.ValidateAsync(setCode, value, ct)).Status;
        _cache[key] = status;
        return status;
    }
}

public sealed class ImportContactsHandler : IRequestHandler<ImportContactsCommand, Response<ImportResultDto>>
{
    private const string DefaultSourceSystem = "default";
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IContactExternalReferenceRepository _externalRefs;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public ImportContactsHandler(
        ITenantContext tenant, IContactRepository contacts, IContactExternalReferenceRepository externalRefs,
        IReferenceDataValidator referenceValidator, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _contacts = contacts;
        _externalRefs = externalRefs;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<ImportResultDto>> Handle(ImportContactsCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ImportResultDto>.Fail("Tenant context is required.", 400);
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var cache = new ReferenceCache(_referenceValidator);
        var errors = new List<ImportRowErrorDto>();
        int valid = 0, created = 0, conflicts = 0;

        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var rowNo = i + 1;
            var rowErrors = new List<ImportRowErrorDto>();

            var hasName = !string.IsNullOrWhiteSpace(row.FirstName) || !string.IsNullOrWhiteSpace(row.LastName);
            if (!hasName)
            {
                rowErrors.Add(new(rowNo, "Name", "required", "At least one of FirstName or LastName is required.", "error"));
            }

            await CheckRefAsync(cache, rowNo, "ContactType", ContactReferenceValidation.ContactTypeSet, row.ContactType, rowErrors, cancellationToken);
            await CheckRefAsync(cache, rowNo, "Status", ContactReferenceValidation.ContactStatusSet, row.Status, rowErrors, cancellationToken);

            if (!string.IsNullOrWhiteSpace(row.Email) && !IsValidEmail(row.Email!))
            {
                rowErrors.Add(new(rowNo, "Email", "invalid", "Email is not a valid address.", "error"));
            }

            var hasExternal = !string.IsNullOrWhiteSpace(row.ExternalId);
            var sourceSystem = string.IsNullOrWhiteSpace(row.ExternalSourceSystem) ? DefaultSourceSystem : row.ExternalSourceSystem!.Trim();
            var isConflict = false;
            if (hasExternal && rowErrors.Count == 0
                && await _externalRefs.ExistsBySourceExternalAsync(tenantId, sourceSystem, row.ExternalId!.Trim(), excludeId: null, cancellationToken))
            {
                rowErrors.Add(new(rowNo, "ExternalId", "conflict", "An external reference with this SourceSystem + ExternalId already exists.", "conflict"));
                isConflict = true;
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                if (isConflict) conflicts++;
                continue;
            }

            valid++;
            if (request.DryRun)
            {
                continue;
            }

            var contact = new DomainContact
            {
                TenantId = tenantId,
                FirstName = row.FirstName?.Trim() ?? string.Empty,
                LastName = row.LastName?.Trim() ?? string.Empty,
                DisplayName = ContactMapper.ResolveDisplayName(row.DisplayName, row.FirstName, row.LastName),
                ContactType = row.ContactType!.Trim(),
                Status = row.Status!.Trim(),
                ProfessionalTitle = row.ProfessionalTitle?.Trim(),
                Specialty = row.Specialty?.Trim(),
                Department = row.Department?.Trim(),
                Phone = row.Phone?.Trim(),
                Email = row.Email?.Trim().ToLowerInvariant(),
                Notes = row.Notes?.Trim()
            };
            await _contacts.InsertAsync(contact, cancellationToken);

            if (hasExternal)
            {
                await _externalRefs.InsertAsync(new DomainContactRef
                {
                    TenantId = tenantId,
                    ContactId = contact.Id,
                    SourceSystem = sourceSystem,
                    ExternalId = row.ExternalId!.Trim()
                }, cancellationToken);
            }

            created++;
        }

        var invalid = request.Rows.Count - valid;
        // Audit carries counts only — never PII/row payload. Fail-soft is handled inside the publisher.
        await _audit.PublishAsync("crm.contact.imported", tenantId, Guid.Empty,
            $"corr={correlationId} dryRun={request.DryRun} total={request.Rows.Count} created={created}", cancellationToken);

        var result = new ImportResultDto(correlationId, request.DryRun, request.Rows.Count, valid, invalid, created, 0, 0, conflicts, errors);
        return Response<ImportResultDto>.Success(result);
    }

    private static async Task CheckRefAsync(ReferenceCache cache, int rowNo, string field, string setCode, string? value, List<ImportRowErrorDto> errors, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new(rowNo, field, "required", $"'{setCode}' is required.", "error"));
            return;
        }

        switch (await cache.StatusAsync(setCode, value, ct))
        {
            case ReferenceValidationStatus.InvalidValue:
                errors.Add(new(rowNo, field, "invalid_reference", $"'{value}' is not a valid published value of reference set '{setCode}'.", "error"));
                break;
            case ReferenceValidationStatus.SetMissing:
                errors.Add(new(rowNo, field, "set_missing", $"Required reference set '{setCode}' is not published yet.", "error"));
                break;
        }
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new System.Net.Mail.MailAddress(email); return true; }
        catch { return false; }
    }
}

public sealed class ExportContactsHandler : IRequestHandler<ExportContactsQuery, Response<string>>
{
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IContactAuditPublisher _audit;

    public ExportContactsHandler(ITenantContext tenant, IContactRepository contacts, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _contacts = contacts;
        _audit = audit;
    }

    public async Task<Response<string>> Handle(ExportContactsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<string>.Fail("Tenant context is required.", 400);
        }

        var contacts = await _contacts.ListAllAsync(tenantId, cancellationToken);
        var header = ImportTemplates.ContactHeader;
        var rows = contacts.Select(c => (IReadOnlyList<object?>)new object?[]
        {
            string.Empty, string.Empty, c.FirstName, c.LastName, c.DisplayName, c.ContactType,
            c.ProfessionalTitle, c.Specialty, c.Department, c.Phone, c.Email, c.Status, c.Notes
        });

        // PII-safe audit: count + filter metadata only, never the exported payload.
        await _audit.PublishAsync("crm.contact.exported", tenantId, Guid.Empty, $"count={contacts.Count}", cancellationToken);
        return Response<string>.Success(Csv.Build(header, rows));
    }
}
