using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainLink = Diten.CrmService.Domain.Entities.AccountContactLink;

namespace Diten.CrmService.Application.Features.ImportExport.Handlers;

public sealed class ImportAccountContactsHandler : IRequestHandler<ImportAccountContactsCommand, Response<ImportResultDto>>
{
    private const string ContactRoleSet = "contact-role";
    private const string DefaultSourceSystem = "default";
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contacts;
    private readonly IContactExternalReferenceRepository _externalRefs;
    private readonly IAccountContactLinkRepository _links;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public ImportAccountContactsHandler(
        ITenantContext tenant, IAccountRepository accounts, IContactRepository contacts, IContactExternalReferenceRepository externalRefs,
        IAccountContactLinkRepository links, IReferenceDataValidator referenceValidator, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _contacts = contacts;
        _externalRefs = externalRefs;
        _links = links;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<ImportResultDto>> Handle(ImportAccountContactsCommand request, CancellationToken cancellationToken)
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

            var accountId = await ResolveAccountAsync(tenantId, row.AccountId, row.AccountCode, rowNo, rowErrors, cancellationToken);
            var contactId = await ResolveContactAsync(tenantId, row.ContactId, row.ContactExternalSourceSystem, row.ContactExternalId, rowNo, rowErrors, cancellationToken);

            if (string.IsNullOrWhiteSpace(row.RoleCode))
            {
                rowErrors.Add(new(rowNo, "RoleCode", "required", "RoleCode is required.", "error"));
            }
            else
            {
                switch (await cache.StatusAsync(ContactRoleSet, row.RoleCode, cancellationToken))
                {
                    case ReferenceValidationStatus.InvalidValue:
                        rowErrors.Add(new(rowNo, "RoleCode", "invalid_reference", $"'{row.RoleCode}' is not a valid published value of reference set '{ContactRoleSet}'.", "error"));
                        break;
                    case ReferenceValidationStatus.SetMissing:
                        rowErrors.Add(new(rowNo, "RoleCode", "set_missing", $"Required reference set '{ContactRoleSet}' is not published yet.", "error"));
                        break;
                }
            }

            if (row.ValidFrom is { } f && row.ValidTo is { } t && f > t)
            {
                rowErrors.Add(new(rowNo, "Validity", "invalid", "ValidFrom must be on or before ValidTo.", "error"));
            }

            var isConflict = false;
            if (accountId is { } aid && contactId is { } cid && !string.IsNullOrWhiteSpace(row.RoleCode) && rowErrors.Count == 0)
            {
                var role = row.RoleCode!.Trim();
                if (await _links.ExistsActiveAsync(tenantId, aid, cid, role, excludeId: null, cancellationToken))
                {
                    rowErrors.Add(new(rowNo, "RoleCode", "conflict", "This contact is already linked to the account with this role.", "conflict"));
                    isConflict = true;
                }
                else if (row.IsPrimary && await _links.ExistsPrimaryAsync(tenantId, aid, role, excludeId: null, cancellationToken))
                {
                    rowErrors.Add(new(rowNo, "IsPrimary", "conflict", "A primary contact already exists for this account and role.", "conflict"));
                    isConflict = true;
                }
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

            await _links.InsertAsync(new DomainLink
            {
                TenantId = tenantId,
                AccountId = accountId!.Value,
                ContactId = contactId!.Value,
                RoleCode = row.RoleCode!.Trim(),
                IsPrimary = row.IsPrimary,
                Status = "active",
                ValidFrom = row.ValidFrom,
                ValidTo = row.ValidTo,
                Notes = row.Notes?.Trim()
            }, cancellationToken);
            created++;
        }

        var invalid = request.Rows.Count - valid;
        await _audit.PublishAsync("crm.account-contact.imported", tenantId, Guid.Empty,
            $"corr={correlationId} dryRun={request.DryRun} total={request.Rows.Count} created={created}", cancellationToken);
        return Response<ImportResultDto>.Success(new ImportResultDto(correlationId, request.DryRun, request.Rows.Count, valid, invalid, created, 0, 0, conflicts, errors));
    }

    private async Task<Guid?> ResolveAccountAsync(Guid tenantId, Guid? accountId, string? accountCode, int rowNo, List<ImportRowErrorDto> errors, CancellationToken ct)
    {
        if (accountId is { } id)
        {
            if (await _accounts.GetByIdAsync(tenantId, id, ct) is not null) return id;
            errors.Add(new(rowNo, "AccountId", "not_found", "Account not found.", "error"));
            return null;
        }

        if (!string.IsNullOrWhiteSpace(accountCode))
        {
            var account = await _accounts.GetByCodeAsync(tenantId, accountCode.Trim(), ct);
            if (account is not null) return account.Id;
            errors.Add(new(rowNo, "AccountCode", "not_found", $"Account with code '{accountCode}' not found.", "error"));
            return null;
        }

        errors.Add(new(rowNo, "Account", "required", "AccountId or AccountCode is required.", "error"));
        return null;
    }

    private async Task<Guid?> ResolveContactAsync(Guid tenantId, Guid? contactId, string? source, string? externalId, int rowNo, List<ImportRowErrorDto> errors, CancellationToken ct)
    {
        if (contactId is { } id)
        {
            if (await _contacts.GetByIdAsync(tenantId, id, ct) is not null) return id;
            errors.Add(new(rowNo, "ContactId", "not_found", "Contact not found.", "error"));
            return null;
        }

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            var src = string.IsNullOrWhiteSpace(source) ? DefaultSourceSystem : source!.Trim();
            var reference = await _externalRefs.GetBySourceExternalAsync(tenantId, src, externalId.Trim(), ct);
            if (reference is not null) return reference.ContactId;
            errors.Add(new(rowNo, "ContactExternalId", "not_found", "Contact with this external reference not found.", "error"));
            return null;
        }

        errors.Add(new(rowNo, "Contact", "required", "ContactId or ContactExternalId is required.", "error"));
        return null;
    }
}

public sealed class ExportAccountContactsHandler : IRequestHandler<ExportAccountContactsQuery, Response<string>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountContactLinkRepository _links;
    private readonly IContactAuditPublisher _audit;

    public ExportAccountContactsHandler(ITenantContext tenant, IAccountContactLinkRepository links, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _links = links;
        _audit = audit;
    }

    public async Task<Response<string>> Handle(ExportAccountContactsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<string>.Fail("Tenant context is required.", 400);
        }

        var links = await _links.ListAllAsync(tenantId, cancellationToken);
        var rows = links.Select(l => (IReadOnlyList<object?>)new object?[]
        {
            string.Empty, l.AccountId, string.Empty, string.Empty, l.ContactId, l.RoleCode, l.IsPrimary, l.ValidFrom, l.ValidTo, l.Notes
        });

        await _audit.PublishAsync("crm.account-contact.exported", tenantId, Guid.Empty, $"count={links.Count}", cancellationToken);
        return Response<string>.Success(Csv.Build(ImportTemplates.AccountContactHeader, rows));
    }
}
