using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountContact.Handlers;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainAccount = Diten.CrmService.Domain.Entities.Account;
using DomainContact = Diten.CrmService.Domain.Entities.Contact;
using DomainLink = Diten.CrmService.Domain.Entities.AccountContactLink;
using DomainRef = Diten.CrmService.Domain.Entities.ContactExternalReference;

namespace Diten.CrmService.Application.Features.ImportExport.Xlsx;

/// <summary>Upload a Task 1 workbook. <c>DryRun</c> validates and previews without writing anything.</summary>
public sealed record ImportContactWorkbookCommand(
    byte[] Content,
    bool DryRun,
    bool StrictMode,
    ImportCapabilities Capabilities) : IRequest<Response<ImportPreviewDto>>;

/// <summary>
/// MOD-0150 Import/Export Task 2 — the import engine.
///
/// Strategy is <b>validate-all, then apply-valid</b>: every row of the file is validated first, and only then (and
/// only when the caller asked to apply and the file passed the apply gates) are the planned writes executed. A dry-run
/// takes exactly the same path minus the writes, so the preview the user approves is the plan that runs.
///
/// Guarantees carried over from the single-write path — deliberately NOT re-invented here, so import can never be a
/// back door around a UI rule: MOD-0048 reference validation, cross-country reason-required
/// (<see cref="CrossCountryPolicy"/>), active-link/primary uniqueness, in-account reporting rules, and the historical
/// lifecycle (ending a link sets Status=ended + ValidTo; nothing is ever deleted).
/// </summary>
public sealed class ContactWorkbookImportHandler : IRequestHandler<ImportContactWorkbookCommand, Response<ImportPreviewDto>>
{
    private const string DefaultSourceSystem = "default";
    private const string ContactEntity = "Contact";
    private const string LinkEntity = "AccountContactLink";
    private const double MaxErrorRatio = 0.2;

    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IContactExternalReferenceRepository _externalRefs;
    private readonly IAccountContactLinkRepository _links;
    private readonly IAccountRepository _accounts;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public ContactWorkbookImportHandler(
        ITenantContext tenant,
        IContactRepository contacts,
        IContactExternalReferenceRepository externalRefs,
        IAccountContactLinkRepository links,
        IAccountRepository accounts,
        IReferenceDataValidator referenceValidator,
        IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _contacts = contacts;
        _externalRefs = externalRefs;
        _links = links;
        _accounts = accounts;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<ImportPreviewDto>> Handle(ImportContactWorkbookCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ImportPreviewDto>.Fail("Tenant context is required.", 400);
        }

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        using var stream = new MemoryStream(request.Content);
        var workbook = ContactWorkbookReader.Read(stream);

        if (!workbook.IsReadable)
        {
            return Response<ImportPreviewDto>.Success(Empty(correlationId, request, workbook, "file_not_readable"));
        }

        if (workbook.ContactRows.Count > ContactWorkbookSchema.MaxContactRows
            || workbook.LinkRows.Count > ContactWorkbookSchema.MaxLinkRows)
        {
            var tooLarge = workbook with
            {
                FileErrors = workbook.FileErrors.Concat(new[]
                {
                    $"The file has too many rows (limit: {ContactWorkbookSchema.MaxContactRows} contacts / "
                    + $"{ContactWorkbookSchema.MaxLinkRows} account links). Split it and import in batches."
                }).ToList()
            };
            return Response<ImportPreviewDto>.Success(Empty(correlationId, request, tooLarge, "file_too_large"));
        }

        var checker = new ImportReferenceChecker(_referenceValidator);
        var state = new ImportState();

        var contactPlans = await PlanContactsAsync(tenantId, workbook.ContactRows, request.Capabilities, checker, state, ct);
        var linkPlans = await PlanLinksAsync(tenantId, workbook.LinkRows, request.Capabilities, checker, state, ct);

        var rows = contactPlans.Select(p => p.Result).Concat(linkPlans.Select(p => p.Result)).ToList();
        var summary = Summarize(rows, workbook.FileWarnings.Count);

        var (canApply, blockedReason) = EvaluateApplyGates(request.StrictMode, summary, checker);
        var applied = false;

        if (!request.DryRun && canApply)
        {
            await ApplyAsync(tenantId, contactPlans, linkPlans, ct);
            applied = true;
        }

        // PII-safe audit: counts and flags only — never a row payload, name, e-mail, phone, note or file name.
        await _audit.PublishAsync(
            request.DryRun ? "crm.contact.import.dry-run" : "crm.contact.imported",
            tenantId, Guid.Empty,
            $"corr={correlationId} source=xlsx dryRun={request.DryRun} applied={applied} strict={request.StrictMode} "
            + $"rows={summary.TotalRows} creates={summary.Creates} updates={summary.Updates} ends={summary.Ends} "
            + $"skips={summary.Skips} errors={summary.Errors} conflicts={summary.Conflicts}",
            ct);

        return Response<ImportPreviewDto>.Success(new ImportPreviewDto(
            correlationId,
            request.DryRun,
            applied,
            canApply,
            blockedReason,
            request.StrictMode ? "strict" : "apply-valid-rows",
            summary,
            workbook.FileErrors,
            workbook.FileWarnings,
            rows));
    }

    // ---------------------------------------------------------------- contacts

    private async Task<List<ContactPlan>> PlanContactsAsync(
        Guid tenantId, IReadOnlyList<ParsedRow> rows, ImportCapabilities caps,
        ImportReferenceChecker checker, ImportState state, CancellationToken ct)
    {
        var plans = new List<ContactPlan>();

        foreach (var row in rows)
        {
            var operation = ImportOperations.Normalize(row.Get(ContactWorkbookSchema.OperationColumn));
            var label = ImportDisplayLabel.ForContact(row.Get("FirstName"), row.Get("LastName"), row.Get("DisplayName"));

            ImportRowResultDto Result(string status, string? code, string message, IReadOnlyList<string>? changed = null, string? key = null)
                => new(row.Sheet, row.RowNumber, operation, ContactEntity, key, status, code, message,
                    changed ?? Array.Empty<string>(), label, Severity(status));

            if (operation is null)
            {
                // Blank Operation is deliberately a no-op: an exported file is downloaded with an empty Operation
                // column, and reading that as "create everything again" would duplicate the whole address book.
                plans.Add(new ContactPlan(row, Result(ImportRowStatuses.Skip, "operation_missing",
                    "No operation was selected for this row, so it was skipped.")));
                continue;
            }

            if (operation == ImportOperations.Skip)
            {
                plans.Add(new ContactPlan(row, Result(ImportRowStatuses.Skip, "skipped", "Row skipped as requested.")));
                continue;
            }

            if (operation == ImportOperations.Delete)
            {
                plans.Add(new ContactPlan(row, Result(ImportRowStatuses.Error, "unsupported_operation",
                    "Deleting a contact through import is not supported. Set the contact status instead.")));
                continue;
            }

            if (operation == ImportOperations.End)
            {
                plans.Add(new ContactPlan(row, Result(ImportRowStatuses.Error, "unsupported_operation",
                    "'end' applies to account links only; it cannot be used on the Contacts sheet.")));
                continue;
            }

            if (!ImportValues.TryReadGuid(row.Get(ContactWorkbookSchema.ContactIdColumn), out var contactId))
            {
                plans.Add(new ContactPlan(row, Result(ImportRowStatuses.Error, "invalid_identifier",
                    "ContactId is not a valid identifier. Leave it empty when adding a new contact.")));
                continue;
            }

            var sourceSystem = row.Get("ExternalSystem")?.Trim();
            var externalId = row.Get("ExternalId")?.Trim();

            if (operation == ImportOperations.Add)
            {
                var plan = await PlanCreateAsync(tenantId, row, contactId, sourceSystem, externalId, caps, checker, state, Result, ct);
                if (plan.Result.Status is ImportRowStatuses.Error or ImportRowStatuses.Conflict)
                {
                    state.RegisterFailedContact(sourceSystem, externalId);
                }

                plans.Add(plan);
                continue;
            }

            if (operation == ImportOperations.Update)
            {
                plans.Add(await PlanUpdateAsync(tenantId, row, contactId, sourceSystem, externalId, caps, checker, state, Result, ct));
                continue;
            }

            plans.Add(new ContactPlan(row, Result(ImportRowStatuses.Error, "unknown_operation",
                $"'{operation}' is not a supported operation. Use add, update or skip.")));
        }

        return plans;
    }

    private async Task<ContactPlan> PlanCreateAsync(
        Guid tenantId, ParsedRow row, Guid? contactId, string? sourceSystem, string? externalId,
        ImportCapabilities caps, ImportReferenceChecker checker, ImportState state,
        Func<string, string?, string, IReadOnlyList<string>?, string?, ImportRowResultDto> result, CancellationToken ct)
    {
        if (!caps.CanCreateContact)
        {
            return new ContactPlan(row, result(ImportRowStatuses.Error, "permission_denied",
                "You do not have permission to create contacts.", null, null));
        }

        if (contactId is not null)
        {
            return new ContactPlan(row, result(ImportRowStatuses.Error, "contact_id_on_add",
                "ContactId must be empty when adding a new contact. Use operation 'update' to change an existing one.", null, null));
        }

        var errors = new List<string>();
        var firstName = row.Get("FirstName")?.Trim();
        var lastName = row.Get("LastName")?.Trim();
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
        {
            errors.Add("At least one of FirstName or LastName is required.");
        }

        var contactType = row.Get("ContactType")?.Trim();
        var status = row.Get("ContactStatus")?.Trim();
        AddIfError(errors, await checker.CheckAsync(ContactReferenceValidation.ContactTypeSet, contactType, true, ct));
        AddIfError(errors, await checker.CheckAsync(ContactReferenceValidation.ContactStatusSet, status, true, ct));
        await CheckOptionalReferencesAsync(row, checker, errors, ct);
        ValidateShape(row, errors);

        if (errors.Count > 0)
        {
            return new ContactPlan(row, result(ImportRowStatuses.Error, "validation_failed", string.Join(" ", errors), null, null));
        }

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            var system = string.IsNullOrWhiteSpace(sourceSystem) ? DefaultSourceSystem : sourceSystem;
            if (state.PlannedExternalRefs.Contains((system, externalId!))
                || await _externalRefs.ExistsBySourceExternalAsync(tenantId, system, externalId!, null, ct))
            {
                // Value-free on purpose: the external id may itself be personal data.
                return new ContactPlan(row, result(ImportRowStatuses.Conflict, "duplicate_external_reference",
                    "A contact with this external system and external id already exists.", null, null));
            }

            state.PlannedExternalRefs.Add((system, externalId!));
        }

        var contact = new DomainContact
        {
            TenantId = tenantId,
            FirstName = firstName ?? string.Empty,
            LastName = lastName ?? string.Empty,
            DisplayName = ContactMapper.ResolveDisplayName(row.Get("DisplayName"), firstName, lastName),
            ContactType = contactType!,
            Status = status!,
            Gender = row.Get("Gender")?.Trim(),
            ProfessionalTitle = row.Get("ProfessionalTitle")?.Trim(),
            Specialty = row.Get("Specialty")?.Trim(),
            Department = row.Get("Department")?.Trim(),
            CountryRef = row.Get("CountryCode")?.Trim(),
            CityRef = row.Get("CityCode")?.Trim(),
            DistrictRef = row.Get("DistrictCode")?.Trim(),
            AddressLine = row.Get("AddressLine")?.Trim(),
            PostalCode = row.Get("PostalCode")?.Trim(),
            PreferredLanguage = row.Get("PreferredLanguage")?.Trim(),
            PhoneCountryCode = row.Get("PhoneCountryCode")?.Trim(),
            Phone = row.Get("Phone")?.Trim(),
            Email = row.Get("Email")?.Trim().ToLowerInvariant(),
            Notes = row.Get("Notes")?.Trim()
        };

        // The id is allocated now (not at write time) so an AccountLinks row in the SAME file can reference this
        // contact — in the preview as well as in the apply pass.
        state.RegisterPlannedContact(contact, sourceSystem, externalId);

        DomainRef? reference = null;
        if (!string.IsNullOrWhiteSpace(externalId))
        {
            reference = new DomainRef
            {
                TenantId = tenantId,
                ContactId = contact.Id,
                SourceSystem = string.IsNullOrWhiteSpace(sourceSystem) ? DefaultSourceSystem : sourceSystem!.Trim(),
                ExternalId = externalId!
            };
        }

        return new ContactPlan(row,
            result(ImportRowStatuses.Create, null, "A new contact will be created.", null, contact.Id.ToString()),
            NewContact: contact, NewReference: reference);
    }

    private async Task<ContactPlan> PlanUpdateAsync(
        Guid tenantId, ParsedRow row, Guid? contactId, string? sourceSystem, string? externalId,
        ImportCapabilities caps, ImportReferenceChecker checker, ImportState state,
        Func<string, string?, string, IReadOnlyList<string>?, string?, ImportRowResultDto> result, CancellationToken ct)
    {
        if (!caps.CanUpdateContact)
        {
            return new ContactPlan(row, result(ImportRowStatuses.Error, "permission_denied",
                "You do not have permission to update contacts.", null, null));
        }

        // Match priority: ContactId, then (ExternalSystem + ExternalId). E-mail/phone are deliberately NOT match
        // keys — they are not unique in this model, and matching on them could overwrite the wrong person's record.
        DomainContact? contact = null;
        if (contactId is { } id)
        {
            contact = state.FindPlanned(id) ?? await _contacts.GetByIdAsync(tenantId, id, ct);
            if (contact is null)
            {
                return new ContactPlan(row, result(ImportRowStatuses.Error, "not_found",
                    "No contact was found for the ContactId in this row.", null, null));
            }
        }
        else if (!string.IsNullOrWhiteSpace(externalId))
        {
            var system = string.IsNullOrWhiteSpace(sourceSystem) ? DefaultSourceSystem : sourceSystem!.Trim();
            var reference = await _externalRefs.GetBySourceExternalAsync(tenantId, system, externalId!, ct);
            contact = reference is null ? null : await _contacts.GetByIdAsync(tenantId, reference.ContactId, ct);
            if (contact is null)
            {
                return new ContactPlan(row, result(ImportRowStatuses.Error, "not_found",
                    "No contact was found for the external system and external id in this row.", null, null));
            }
        }
        else
        {
            return new ContactPlan(row, result(ImportRowStatuses.Error, "match_key_missing",
                "An update needs a ContactId, or an ExternalSystem and ExternalId pair.", null, null));
        }

        if (state.UpdatedContactIds.Contains(contact.Id))
        {
            return new ContactPlan(row, result(ImportRowStatuses.Conflict, "duplicate_row",
                "This contact is updated by an earlier row in the same file.", null, contact.Id.ToString()));
        }

        var errors = new List<string>();
        var changed = new List<string>();
        var draft = Clone(contact);

        // Reference-validated fields first — an invalid code must fail the row before anything is staged.
        var contactType = ImportValues.ReadOptional(row, "ContactType");
        if (contactType is { } ct2)
        {
            if (ct2.Value is null)
            {
                errors.Add("ContactType is required and cannot be cleared.");
            }
            else
            {
                AddIfError(errors, await checker.CheckAsync(ContactReferenceValidation.ContactTypeSet, ct2.Value, true, ct));
                Set(draft, changed, "ContactType", ct2.Value, () => draft.ContactType, v => draft.ContactType = v!);
            }
        }

        var statusValue = ImportValues.ReadOptional(row, "ContactStatus");
        if (statusValue is { } st)
        {
            if (st.Value is null)
            {
                errors.Add("ContactStatus is required and cannot be cleared.");
            }
            else
            {
                AddIfError(errors, await checker.CheckAsync(ContactReferenceValidation.ContactStatusSet, st.Value, true, ct));
                Set(draft, changed, "ContactStatus", st.Value, () => draft.Status, v => draft.Status = v!);
            }
        }

        await ApplyOptionalReferenceUpdateAsync(row, "Gender", ContactReferenceValidation.GenderSet, draft, changed, errors, checker, v => draft.Gender = v, () => draft.Gender, ct);
        await ApplyOptionalReferenceUpdateAsync(row, "ProfessionalTitle", ContactReferenceValidation.ProfessionalTitleSet, draft, changed, errors, checker, v => draft.ProfessionalTitle = v, () => draft.ProfessionalTitle, ct);
        await ApplyOptionalReferenceUpdateAsync(row, "Specialty", ContactReferenceValidation.MedicalSpecialtySet, draft, changed, errors, checker, v => draft.Specialty = v, () => draft.Specialty, ct);
        await ApplyOptionalReferenceUpdateAsync(row, "Department", ContactReferenceValidation.DepartmentTypeSet, draft, changed, errors, checker, v => draft.Department = v, () => draft.Department, ct);
        await ApplyOptionalReferenceUpdateAsync(row, "CountryCode", ContactReferenceValidation.CountrySet, draft, changed, errors, checker, v => draft.CountryRef = v, () => draft.CountryRef, ct);
        await ApplyOptionalReferenceUpdateAsync(row, "CityCode", ContactReferenceValidation.CitySet, draft, changed, errors, checker, v => draft.CityRef = v, () => draft.CityRef, ct);
        await ApplyOptionalReferenceUpdateAsync(row, "DistrictCode", ContactReferenceValidation.DistrictSet, draft, changed, errors, checker, v => draft.DistrictRef = v, () => draft.DistrictRef, ct);
        await ApplyOptionalReferenceUpdateAsync(row, "PreferredLanguage", ContactWorkbookSchema.PreferredLanguageSet, draft, changed, errors, checker, v => draft.PreferredLanguage = v, () => draft.PreferredLanguage, ct);
        await ApplyOptionalReferenceUpdateAsync(row, "PhoneCountryCode", ContactWorkbookSchema.PhoneCountryCodeSet, draft, changed, errors, checker, v => draft.PhoneCountryCode = v, () => draft.PhoneCountryCode, ct);

        SetOptional(row, "FirstName", draft, changed, () => draft.FirstName, v => draft.FirstName = v ?? string.Empty);
        SetOptional(row, "LastName", draft, changed, () => draft.LastName, v => draft.LastName = v ?? string.Empty);
        SetOptional(row, "DisplayName", draft, changed, () => draft.DisplayName, v => draft.DisplayName = v ?? string.Empty);
        SetOptional(row, "AddressLine", draft, changed, () => draft.AddressLine, v => draft.AddressLine = v);
        SetOptional(row, "PostalCode", draft, changed, () => draft.PostalCode, v => draft.PostalCode = v);
        SetOptional(row, "Phone", draft, changed, () => draft.Phone, v => draft.Phone = v);
        SetOptional(row, "Notes", draft, changed, () => draft.Notes, v => draft.Notes = v);

        var email = ImportValues.ReadOptional(row, "Email");
        if (email is { } em)
        {
            Set(draft, changed, "Email", em.Value?.ToLowerInvariant(), () => draft.Email, v => draft.Email = v);
        }

        if (string.IsNullOrWhiteSpace(draft.FirstName) && string.IsNullOrWhiteSpace(draft.LastName))
        {
            errors.Add("At least one of FirstName or LastName is required.");
        }

        // DisplayName keeps deriving itself when the user blanks it out (single-write parity).
        if (string.IsNullOrWhiteSpace(draft.DisplayName))
        {
            draft.DisplayName = ContactMapper.ResolveDisplayName(null, draft.FirstName, draft.LastName);
        }

        ValidateShapeOn(draft, errors);

        var warnings = new List<string>();
        if (contactId is not null && !string.IsNullOrWhiteSpace(externalId))
        {
            // Changing an external identity is an identity operation, not a field edit — ignored with a warning
            // rather than silently rewriting which legacy record this contact is.
            var system = string.IsNullOrWhiteSpace(sourceSystem) ? DefaultSourceSystem : sourceSystem!.Trim();
            var existing = await _externalRefs.ListByContactAsync(tenantId, contact.Id, ct);
            if (!existing.Any(r => string.Equals(r.SourceSystem, system, StringComparison.OrdinalIgnoreCase)
                                   && string.Equals(r.ExternalId, externalId, StringComparison.Ordinal)))
            {
                warnings.Add("External reference changes are ignored by import.");
            }
        }

        if (errors.Count > 0)
        {
            return new ContactPlan(row, result(ImportRowStatuses.Error, "validation_failed", string.Join(" ", errors), changed, contact.Id.ToString()));
        }

        state.UpdatedContactIds.Add(contact.Id);

        if (changed.Count == 0)
        {
            return new ContactPlan(row, result(ImportRowStatuses.Skip, "no_change",
                "Nothing to update: no field differs from the stored contact.", null, contact.Id.ToString()));
        }

        var message = "The contact will be updated." + (warnings.Count > 0 ? " " + string.Join(" ", warnings) : string.Empty);
        return new ContactPlan(row,
            result(ImportRowStatuses.Update, warnings.Count > 0 ? "updated_with_warning" : null, message, changed, contact.Id.ToString()),
            ExistingContact: contact, Draft: draft);
    }

    // ---------------------------------------------------------------- links

    private async Task<List<LinkPlan>> PlanLinksAsync(
        Guid tenantId, IReadOnlyList<ParsedRow> rows, ImportCapabilities caps,
        ImportReferenceChecker checker, ImportState state, CancellationToken ct)
    {
        var plans = new List<LinkPlan>();

        foreach (var row in rows)
        {
            var operation = ImportOperations.Normalize(row.Get(ContactWorkbookSchema.OperationColumn));

            ImportRowResultDto Result(string status, string? code, string message, IReadOnlyList<string>? changed = null, string? key = null)
                => new(row.Sheet, row.RowNumber, operation, LinkEntity, key, status, code, message,
                    changed ?? Array.Empty<string>(), null, Severity(status));

            if (operation is null)
            {
                plans.Add(new LinkPlan(row, Result(ImportRowStatuses.Skip, "operation_missing",
                    "No operation was selected for this row, so it was skipped.")));
                continue;
            }

            if (operation == ImportOperations.Skip)
            {
                plans.Add(new LinkPlan(row, Result(ImportRowStatuses.Skip, "skipped", "Row skipped as requested.")));
                continue;
            }

            if (operation == ImportOperations.Delete)
            {
                plans.Add(new LinkPlan(row, Result(ImportRowStatuses.Error, "unsupported_operation",
                    "Deleting an account link is not supported. Use operation 'end' to close it historically.")));
                continue;
            }

            if (!caps.CanManageLinks)
            {
                plans.Add(new LinkPlan(row, Result(ImportRowStatuses.Error, "permission_denied",
                    "You do not have permission to manage account contact links.")));
                continue;
            }

            plans.Add(operation switch
            {
                ImportOperations.Add => await PlanLinkAddAsync(tenantId, row, checker, state, Result, ct),
                ImportOperations.Update => await PlanLinkUpdateAsync(tenantId, row, checker, state, Result, ct),
                ImportOperations.End => await PlanLinkEndAsync(tenantId, row, state, Result, ct),
                _ => new LinkPlan(row, Result(ImportRowStatuses.Error, "unknown_operation",
                    $"'{operation}' is not a supported operation. Use add, update, end or skip."))
            });
        }

        return plans;
    }

    private async Task<LinkPlan> PlanLinkAddAsync(
        Guid tenantId, ParsedRow row, ImportReferenceChecker checker, ImportState state,
        Func<string, string?, string, IReadOnlyList<string>?, string?, ImportRowResultDto> result, CancellationToken ct)
    {
        var resolution = await ResolveLinkTargetsAsync(tenantId, row, state, ct);
        if (resolution.Error is { } targetError)
        {
            return new LinkPlan(row, result(resolution.Status, resolution.Code, targetError, null, null));
        }

        var contact = resolution.Contact!;
        var account = resolution.Account!;
        var errors = new List<string>();

        var roleCode = row.Get("RoleCode")?.Trim();
        AddIfError(errors, await checker.CheckAsync(ContactWorkbookSchema.ContactRoleSet, roleCode, true, ct));

        if (!ImportValues.TryReadDate(row.Get("ValidFrom"), out var validFrom))
        {
            errors.Add("ValidFrom is not a valid date. Use the yyyy-MM-dd format.");
        }

        if (!ImportValues.TryReadDate(row.Get("ValidTo"), out var validTo))
        {
            errors.Add("ValidTo is not a valid date. Use the yyyy-MM-dd format.");
        }

        if (AccountContactValidation.ValidateValidity(validFrom, validTo) is { } validityError)
        {
            errors.Add(validityError);
        }

        var status = row.Get("Status")?.Trim();
        if (!string.IsNullOrWhiteSpace(status) && !ImportValues.IsAllowedLinkStatus(status))
        {
            errors.Add($"Status must be one of: {string.Join(", ", ImportValues.AllowedLinkStatuses)}.");
        }

        var reason = row.Get("CrossCountryReason")?.Trim();
        var crossCountry = CrossCountryPolicy.Evaluate(contact.CountryRef, account.CountryRef, reason);
        if (crossCountry.ReasonRequiredButMissing)
        {
            errors.Add("This contact and account are in different countries. Provide a business reason (CrossCountryReason).");
        }

        if (!ImportValues.TryReadGuid(row.Get("ReportsToContactId"), out var reportsTo))
        {
            errors.Add("ReportsToContactId is not a valid identifier.");
        }
        else if (reportsTo is not null
                 && await AccountContactValidation.ValidateReportsToAsync(
                     _links, tenantId, account.Id, contact.Id, reportsTo, null, ct) is { } reportsToError)
        {
            errors.Add(reportsToError);
        }

        if (errors.Count > 0)
        {
            return new LinkPlan(row, result(ImportRowStatuses.Error, "validation_failed", string.Join(" ", errors), null, null));
        }

        var role = roleCode!;
        var isPrimary = ImportValues.ReadBool(row.Get("IsPrimary")) ?? false;

        // Uniqueness counts ACTIVE records only — a historically ended link must never block a new active one
        // (the doctor who comes back). Planned rows in this same file count too.
        if (state.PlannedActiveLinks.Contains((account.Id, contact.Id, role.ToLowerInvariant()))
            || await _links.ExistsActiveAsync(tenantId, account.Id, contact.Id, role, null, ct))
        {
            return new LinkPlan(row, result(ImportRowStatuses.Conflict, "duplicate_link",
                "This contact is already linked to the account with this role.", null, null));
        }

        if (isPrimary
            && (state.PlannedPrimaries.Contains((account.Id, role.ToLowerInvariant()))
                || await _links.ExistsPrimaryAsync(tenantId, account.Id, role, null, ct)))
        {
            return new LinkPlan(row, result(ImportRowStatuses.Conflict, "second_primary",
                "A primary contact already exists for this account and role.", null, null));
        }

        state.PlannedActiveLinks.Add((account.Id, contact.Id, role.ToLowerInvariant()));
        if (isPrimary)
        {
            state.PlannedPrimaries.Add((account.Id, role.ToLowerInvariant()));
        }

        var link = new DomainLink
        {
            TenantId = tenantId,
            AccountId = account.Id,
            ContactId = contact.Id,
            RoleCode = role,
            IsPrimary = isPrimary,
            Status = string.IsNullOrWhiteSpace(status) ? "active" : status!.ToLowerInvariant(),
            ValidFrom = validFrom,
            ValidTo = validTo,
            Notes = row.Get("Notes")?.Trim(),
            CrossCountryReason = crossCountry.IsCrossCountry ? reason : null,
            ReportsToContactId = reportsTo
        };

        var note = crossCountry.IsCrossCountry ? " Cross-country link (a business reason was provided)." : string.Empty;
        return new LinkPlan(row,
            result(ImportRowStatuses.Create, null, "A new account link will be created." + note, null, link.Id.ToString()),
            NewLink: link);
    }

    private async Task<LinkPlan> PlanLinkUpdateAsync(
        Guid tenantId, ParsedRow row, ImportReferenceChecker checker, ImportState state,
        Func<string, string?, string, IReadOnlyList<string>?, string?, ImportRowResultDto> result, CancellationToken ct)
    {
        var match = await MatchLinkAsync(tenantId, row, state, ct);
        if (match.Error is { } matchError)
        {
            return new LinkPlan(row, result(ImportRowStatuses.Error, match.Code, matchError, null, null));
        }

        var link = match.Link!;
        var errors = new List<string>();
        var changed = new List<string>();
        var draft = Clone(link);

        // The identity of a link (which contact, which account) is immutable: re-pointing it would rewrite history
        // instead of recording a change. Moving a contact = end the old link + add a new one.
        if (ImportValues.TryReadGuid(row.Get("AccountId"), out var accountId) && accountId is { } aid && aid != link.AccountId)
        {
            errors.Add("AccountId cannot be changed on an existing link. End this link and add a new one instead.");
        }

        if (ImportValues.TryReadGuid(row.Get(ContactWorkbookSchema.ContactIdColumn), out var contactId) && contactId is { } cid && cid != link.ContactId)
        {
            errors.Add("ContactId cannot be changed on an existing link. End this link and add a new one instead.");
        }

        var roleCode = ImportValues.ReadOptional(row, "RoleCode");
        if (roleCode is { } rc)
        {
            if (rc.Value is null)
            {
                errors.Add("RoleCode is required and cannot be cleared.");
            }
            else
            {
                AddIfError(errors, await checker.CheckAsync(ContactWorkbookSchema.ContactRoleSet, rc.Value, true, ct));
                Set(draft, changed, "RoleCode", rc.Value, () => draft.RoleCode, v => draft.RoleCode = v!);
            }
        }

        var isPrimary = ImportValues.ReadBool(row.Get("IsPrimary"));
        if (isPrimary is { } primary && primary != draft.IsPrimary)
        {
            draft.IsPrimary = primary;
            changed.Add("IsPrimary");
        }

        var status = ImportValues.ReadOptional(row, "Status");
        if (status is { } st && st.Value is not null)
        {
            if (!ImportValues.IsAllowedLinkStatus(st.Value))
            {
                errors.Add($"Status must be one of: {string.Join(", ", ImportValues.AllowedLinkStatuses)}.");
            }
            else
            {
                Set(draft, changed, "Status", st.Value.ToLowerInvariant(), () => draft.Status, v => draft.Status = v!);
            }
        }

        if (ImportValues.TryReadDate(row.Get("ValidFrom"), out var validFrom) && validFrom is not null && validFrom != draft.ValidFrom)
        {
            draft.ValidFrom = validFrom;
            changed.Add("ValidFrom");
        }

        if (ImportValues.TryReadDate(row.Get("ValidTo"), out var validTo) && validTo is not null && validTo != draft.ValidTo)
        {
            draft.ValidTo = validTo;
            changed.Add("ValidTo");
        }

        if (AccountContactValidation.ValidateValidity(draft.ValidFrom, draft.ValidTo) is { } validityError)
        {
            errors.Add(validityError);
        }

        SetOptional(row, "Notes", draft, changed, () => draft.Notes, v => draft.Notes = v);
        SetOptional(row, "CrossCountryReason", draft, changed, () => draft.CrossCountryReason, v => draft.CrossCountryReason = v);

        // Cross-country is re-evaluated on update too: an edit must not be a way to drop the required justification.
        var contact = await _contacts.GetByIdAsync(tenantId, draft.ContactId, ct);
        var account = await _accounts.GetByIdAsync(tenantId, draft.AccountId, ct);
        var crossCountry = CrossCountryPolicy.Evaluate(contact?.CountryRef, account?.CountryRef, draft.CrossCountryReason);
        if (crossCountry.ReasonRequiredButMissing)
        {
            errors.Add("This contact and account are in different countries. Provide a business reason (CrossCountryReason).");
        }

        if (ImportValues.TryReadGuid(row.Get("ReportsToContactId"), out var reportsTo) && reportsTo != draft.ReportsToContactId)
        {
            if (await AccountContactValidation.ValidateReportsToAsync(
                    _links, tenantId, draft.AccountId, draft.ContactId, reportsTo, draft.Id, ct) is { } reportsToError)
            {
                errors.Add(reportsToError);
            }
            else
            {
                draft.ReportsToContactId = reportsTo;
                changed.Add("ReportsToContactId");
            }
        }

        if (errors.Count > 0)
        {
            return new LinkPlan(row, result(ImportRowStatuses.Error, "validation_failed", string.Join(" ", errors), changed, link.Id.ToString()));
        }

        if (changed.Count == 0)
        {
            return new LinkPlan(row, result(ImportRowStatuses.Skip, "no_change",
                "Nothing to update: no field differs from the stored link.", null, link.Id.ToString()));
        }

        return new LinkPlan(row,
            result(ImportRowStatuses.Update, null, "The account link will be updated.", changed, link.Id.ToString()),
            ExistingLink: link, LinkDraft: draft);
    }

    private async Task<LinkPlan> PlanLinkEndAsync(
        Guid tenantId, ParsedRow row, ImportState state,
        Func<string, string?, string, IReadOnlyList<string>?, string?, ImportRowResultDto> result, CancellationToken ct)
    {
        var match = await MatchLinkAsync(tenantId, row, state, ct);
        if (match.Error is { } matchError)
        {
            return new LinkPlan(row, result(ImportRowStatuses.Error, match.Code, matchError, null, null));
        }

        var link = match.Link!;

        if (!ImportValues.TryReadDate(row.Get("ValidTo"), out var validTo))
        {
            return new LinkPlan(row, result(ImportRowStatuses.Error, "invalid_date",
                "ValidTo is not a valid date. Use the yyyy-MM-dd format.", null, link.Id.ToString()));
        }

        if (validTo is null)
        {
            return new LinkPlan(row, result(ImportRowStatuses.Error, "end_requires_validto",
                "Ending a link requires an end date in ValidTo.", null, link.Id.ToString()));
        }

        if (link.ValidFrom is { } from && from > validTo)
        {
            return new LinkPlan(row, result(ImportRowStatuses.Error, "invalid_validity",
                "The end date cannot be before the link's start date.", null, link.Id.ToString()));
        }

        if (RelationshipLifecycle.IsClosed(link.Status))
        {
            return new LinkPlan(row, result(ImportRowStatuses.Skip, "already_ended",
                "This link is already closed; it was left untouched.", null, link.Id.ToString()));
        }

        var draft = Clone(link);
        draft.Status = RelationshipLifecycle.ClosedStatuses[0]; // "ended"
        draft.ValidTo = validTo;

        // The record itself survives: IsDeleted stays false and the row keeps its id, so downstream sales/visit/order/
        // route history that points at this link keeps resolving.
        return new LinkPlan(row,
            result(ImportRowStatuses.End, null,
                "The link will be closed historically (status ended, end date set). The record is kept, not deleted.",
                new[] { "Status", "ValidTo" }, link.Id.ToString()),
            ExistingLink: link, LinkDraft: draft);
    }

    // ---------------------------------------------------------------- resolution helpers

    private async Task<LinkTargets> ResolveLinkTargetsAsync(Guid tenantId, ParsedRow row, ImportState state, CancellationToken ct)
    {
        if (!ImportValues.TryReadGuid(row.Get(ContactWorkbookSchema.ContactIdColumn), out var contactId))
        {
            return LinkTargets.Failure(ImportRowStatuses.Error, "invalid_identifier", "ContactId is not a valid identifier.");
        }

        DomainContact? contact = null;
        var dependsOnPlanned = false;

        if (contactId is { } cid)
        {
            contact = state.FindPlanned(cid);
            dependsOnPlanned = contact is not null;
            contact ??= await _contacts.GetByIdAsync(tenantId, cid, ct);
        }
        else
        {
            var system = row.Get("ContactExternalSystem")?.Trim();
            var externalId = row.Get("ContactExternalId")?.Trim();
            if (!string.IsNullOrWhiteSpace(externalId))
            {
                var key = (string.IsNullOrWhiteSpace(system) ? DefaultSourceSystem : system!, externalId!);

                if (state.FailedContactKeys.Contains(key))
                {
                    return LinkTargets.Failure(ImportRowStatuses.SkippedDependency, "skipped_dependency",
                        "The contact this link depends on could not be imported, so this row was skipped.");
                }

                contact = state.FindPlannedByExternal(key);
                dependsOnPlanned = contact is not null;

                if (contact is null)
                {
                    var reference = await _externalRefs.GetBySourceExternalAsync(tenantId, key.Item1, key.Item2, ct);
                    contact = reference is null ? null : await _contacts.GetByIdAsync(tenantId, reference.ContactId, ct);
                }
            }
        }

        if (contact is null)
        {
            return LinkTargets.Failure(ImportRowStatuses.Error, "contact_not_found",
                "No contact was found for this row. Provide a ContactId, or an external system and external id.");
        }

        var account = await ResolveAccountAsync(tenantId, row, ct);
        if (account is null)
        {
            return LinkTargets.Failure(ImportRowStatuses.Error, "account_not_found",
                "No account was found for this row. Provide a valid AccountId or AccountCode.");
        }

        return new LinkTargets(contact, account, dependsOnPlanned, null, ImportRowStatuses.Error, null);
    }

    private async Task<DomainAccount?> ResolveAccountAsync(Guid tenantId, ParsedRow row, CancellationToken ct)
    {
        if (ImportValues.TryReadGuid(row.Get("AccountId"), out var accountId) && accountId is { } aid)
        {
            return await _accounts.GetByIdAsync(tenantId, aid, ct);
        }

        var code = row.Get("AccountCode")?.Trim();
        return string.IsNullOrWhiteSpace(code) ? null : await _accounts.GetByCodeAsync(tenantId, code, ct);
    }

    /// <summary>LinkId → (Contact + Account + Role, active) → (contact external ref + AccountCode + Role, active).</summary>
    private async Task<LinkMatch> MatchLinkAsync(Guid tenantId, ParsedRow row, ImportState state, CancellationToken ct)
    {
        if (!ImportValues.TryReadGuid(row.Get(ContactWorkbookSchema.LinkIdColumn), out var linkId))
        {
            return LinkMatch.Failure("invalid_identifier", "LinkId is not a valid identifier.");
        }

        if (linkId is { } id)
        {
            var link = await _links.GetByIdAsync(tenantId, id, ct);
            return link is null
                ? LinkMatch.Failure("not_found", "No account link was found for the LinkId in this row.")
                : new LinkMatch(link, null, null);
        }

        var targets = await ResolveLinkTargetsAsync(tenantId, row, state, ct);
        if (targets.Error is not null)
        {
            return LinkMatch.Failure(targets.Code ?? "not_found", targets.Error);
        }

        var role = row.Get("RoleCode")?.Trim();
        if (string.IsNullOrWhiteSpace(role))
        {
            return LinkMatch.Failure("match_key_missing",
                "Provide a LinkId, or a contact, account and RoleCode so the existing link can be found.");
        }

        var candidates = (await _links.ListByAccountAsync(tenantId, targets.Account!.Id, ct))
            .Where(l => l.ContactId == targets.Contact!.Id
                        && string.Equals(l.RoleCode, role, StringComparison.OrdinalIgnoreCase)
                        && !RelationshipLifecycle.IsClosed(l.Status))
            .ToList();

        return candidates.Count switch
        {
            0 => LinkMatch.Failure("not_found", "No active account link was found for this contact, account and role."),
            1 => new LinkMatch(candidates[0], null, null),
            _ => LinkMatch.Failure("ambiguous_match",
                "More than one active link matches this contact, account and role. Use LinkId to be explicit.")
        };
    }

    // ---------------------------------------------------------------- apply

    private async Task ApplyAsync(Guid tenantId, List<ContactPlan> contactPlans, List<LinkPlan> linkPlans, CancellationToken ct)
    {
        // Contacts first: an AccountLinks row may point at a contact created by this same file.
        foreach (var plan in contactPlans)
        {
            if (plan.NewContact is { } created)
            {
                await _contacts.InsertAsync(created, ct);
                if (plan.NewReference is { } reference)
                {
                    await _externalRefs.InsertAsync(reference, ct);
                }

                await _audit.PublishAsync(ContactAuditEvents.Create, tenantId, created.Id, detail: "source=import", ct);
            }
            else if (plan is { ExistingContact: { } existing, Draft: { } draft })
            {
                CopyInto(draft, existing);
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await _contacts.UpdateAsync(existing, ct);
                await _audit.PublishAsync(ContactAuditEvents.Update, tenantId, existing.Id, detail: "source=import", ct);
            }
        }

        foreach (var plan in linkPlans)
        {
            if (plan.NewLink is { } link)
            {
                await _links.InsertAsync(link, ct);
                await _audit.PublishAsync("account-contact.link", tenantId, link.ContactId,
                    $"account={link.AccountId} source=import", ct);
            }
            else if (plan is { ExistingLink: { } existing, LinkDraft: { } draft })
            {
                CopyInto(draft, existing);
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                // ReplaceOne on the same id — the historical record keeps its identity; nothing is deleted.
                await _links.UpdateAsync(existing, ct);
                await _audit.PublishAsync(
                    RelationshipLifecycle.IsClosed(existing.Status) ? "account-contact.link.ended" : "account-contact.link.updated",
                    tenantId, existing.ContactId, $"account={existing.AccountId} source=import", ct);
            }
        }
    }

    private (bool CanApply, string? Reason) EvaluateApplyGates(bool strict, ImportSummaryDto summary, ImportReferenceChecker checker)
    {
        if (checker.MissingRequiredSets.Count > 0)
        {
            return (false, $"Required reference data is not published yet: {string.Join(", ", checker.MissingRequiredSets)}. "
                           + "Ask an administrator to publish it, then import again.");
        }

        var actionable = summary.Creates + summary.Updates + summary.Ends;
        if (actionable == 0)
        {
            return (false, "There is nothing to apply: no row would create, update or end a record.");
        }

        if (strict && summary.Errors + summary.Conflicts > 0)
        {
            return (false, "Strict mode is on and the file still has errors, so nothing was applied.");
        }

        if (summary.TotalRows > 0 && (double)(summary.Errors + summary.Conflicts) / summary.TotalRows > MaxErrorRatio)
        {
            return (false, "More than 20% of the rows have errors. The file looks wrong — fix the errors and try again.");
        }

        return (true, null);
    }

    // ---------------------------------------------------------------- small helpers

    private static ImportSummaryDto Summarize(IReadOnlyList<ImportRowResultDto> rows, int fileWarnings)
        => new(
            rows.Count,
            rows.Count(r => r.Status == ImportRowStatuses.Create),
            rows.Count(r => r.Status == ImportRowStatuses.Update),
            rows.Count(r => r.Status == ImportRowStatuses.End),
            rows.Count(r => r.Status is ImportRowStatuses.Skip or ImportRowStatuses.SkippedDependency),
            rows.Count(r => r.Status == ImportRowStatuses.Error),
            fileWarnings + rows.Count(r => r.Severity == "warning"),
            rows.Count(r => r.Status == ImportRowStatuses.Conflict));

    private static string Severity(string status) => status switch
    {
        ImportRowStatuses.Error => "error",
        ImportRowStatuses.Conflict => "conflict",
        ImportRowStatuses.SkippedDependency => "warning",
        _ => "info"
    };

    private ImportPreviewDto Empty(string correlationId, ImportContactWorkbookCommand request, ParsedWorkbook workbook, string reason)
        => new(correlationId, request.DryRun, false, false, reason, request.StrictMode ? "strict" : "apply-valid-rows",
            new ImportSummaryDto(0, 0, 0, 0, 0, workbook.FileErrors.Count, workbook.FileWarnings.Count, 0),
            workbook.FileErrors, workbook.FileWarnings, Array.Empty<ImportRowResultDto>());

    private static void AddIfError(List<string> errors, string? error)
    {
        if (error is not null)
        {
            errors.Add(error);
        }
    }

    private async Task CheckOptionalReferencesAsync(ParsedRow row, ImportReferenceChecker checker, List<string> errors, CancellationToken ct)
    {
        foreach (var (column, setCode) in ContactWorkbookSchema.ContactColumnSets)
        {
            if (setCode is ContactReferenceValidation.ContactTypeSet or ContactReferenceValidation.ContactStatusSet)
            {
                continue; // required ones are checked explicitly by the caller
            }

            AddIfError(errors, await checker.CheckAsync(setCode, row.Get(column), false, ct));
        }
    }

    private static void ValidateShape(ParsedRow row, List<string> errors)
    {
        MaxLen(errors, row.Get("FirstName"), 120, "FirstName");
        MaxLen(errors, row.Get("LastName"), 120, "LastName");
        MaxLen(errors, row.Get("DisplayName"), 200, "DisplayName");
        MaxLen(errors, row.Get("Phone"), 32, "Phone");
        MaxLen(errors, row.Get("AddressLine"), 256, "AddressLine");
        MaxLen(errors, row.Get("PostalCode"), 16, "PostalCode");
        MaxLen(errors, row.Get("Notes"), 2000, "Notes");

        var email = row.Get("Email");
        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
        {
            errors.Add("Email is not a valid address.");
        }
    }

    private static void ValidateShapeOn(DomainContact contact, List<string> errors)
    {
        MaxLen(errors, contact.FirstName, 120, "FirstName");
        MaxLen(errors, contact.LastName, 120, "LastName");
        MaxLen(errors, contact.DisplayName, 200, "DisplayName");
        MaxLen(errors, contact.Phone, 32, "Phone");
        MaxLen(errors, contact.AddressLine, 256, "AddressLine");
        MaxLen(errors, contact.PostalCode, 16, "PostalCode");
        MaxLen(errors, contact.Notes, 2000, "Notes");

        if (!string.IsNullOrWhiteSpace(contact.Email) && !IsValidEmail(contact.Email))
        {
            errors.Add("Email is not a valid address.");
        }
    }

    private static void MaxLen(List<string> errors, string? value, int max, string field)
    {
        if (value is not null && value.Length > max)
        {
            errors.Add($"{field} is longer than {max} characters.");
        }
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new System.Net.Mail.MailAddress(email); return true; }
        catch { return false; }
    }

    private async Task ApplyOptionalReferenceUpdateAsync(
        ParsedRow row, string column, string setCode, DomainContact draft, List<string> changed, List<string> errors,
        ImportReferenceChecker checker, Action<string?> setter, Func<string?> getter, CancellationToken ct)
    {
        var value = ImportValues.ReadOptional(row, column);
        if (value is not { } v)
        {
            return;
        }

        if (v.Value is not null)
        {
            AddIfError(errors, await checker.CheckAsync(setCode, v.Value, false, ct));
        }

        if (!string.Equals(getter(), v.Value, StringComparison.Ordinal))
        {
            setter(v.Value);
            changed.Add(column);
        }
    }

    private static void SetOptional(ParsedRow row, string column, object _, List<string> changed, Func<string?> getter, Action<string?> setter)
    {
        var value = ImportValues.ReadOptional(row, column);
        if (value is not { } v)
        {
            return;
        }

        if (!string.Equals(getter(), v.Value, StringComparison.Ordinal))
        {
            setter(v.Value);
            changed.Add(column);
        }
    }

    private static void Set(object _, List<string> changed, string column, string? value, Func<string?> getter, Action<string?> setter)
    {
        if (!string.Equals(getter(), value, StringComparison.Ordinal))
        {
            setter(value);
            changed.Add(column);
        }
    }

    private static DomainContact Clone(DomainContact c) => new()
    {
        Id = c.Id, TenantId = c.TenantId, IsDeleted = c.IsDeleted, DeletedAt = c.DeletedAt, CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt, Version = c.Version, FirstName = c.FirstName, LastName = c.LastName,
        DisplayName = c.DisplayName, ContactType = c.ContactType, Status = c.Status, Gender = c.Gender,
        ProfessionalTitle = c.ProfessionalTitle, Specialty = c.Specialty, Department = c.Department,
        Phone = c.Phone, Email = c.Email, Notes = c.Notes, PhotoDataUri = c.PhotoDataUri,
        CountryRef = c.CountryRef, CityRef = c.CityRef, DistrictRef = c.DistrictRef, AddressLine = c.AddressLine,
        PostalCode = c.PostalCode, PreferredLanguage = c.PreferredLanguage, PhoneCountryCode = c.PhoneCountryCode
    };

    private static void CopyInto(DomainContact from, DomainContact to)
    {
        to.FirstName = from.FirstName; to.LastName = from.LastName; to.DisplayName = from.DisplayName;
        to.ContactType = from.ContactType; to.Status = from.Status; to.Gender = from.Gender;
        to.ProfessionalTitle = from.ProfessionalTitle; to.Specialty = from.Specialty; to.Department = from.Department;
        to.Phone = from.Phone; to.Email = from.Email; to.Notes = from.Notes;
        to.CountryRef = from.CountryRef; to.CityRef = from.CityRef; to.DistrictRef = from.DistrictRef;
        to.AddressLine = from.AddressLine; to.PostalCode = from.PostalCode;
        to.PreferredLanguage = from.PreferredLanguage; to.PhoneCountryCode = from.PhoneCountryCode;
        // PhotoDataUri is intentionally not importable (personal data, excluded from the workbook schema).
    }

    private static DomainLink Clone(DomainLink l) => new()
    {
        Id = l.Id, TenantId = l.TenantId, IsDeleted = l.IsDeleted, DeletedAt = l.DeletedAt, CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt, Version = l.Version, AccountId = l.AccountId, ContactId = l.ContactId,
        RoleCode = l.RoleCode, IsPrimary = l.IsPrimary, Status = l.Status, ValidFrom = l.ValidFrom, ValidTo = l.ValidTo,
        Notes = l.Notes, ReportsToContactId = l.ReportsToContactId, CrossCountryReason = l.CrossCountryReason
    };

    private static void CopyInto(DomainLink from, DomainLink to)
    {
        to.RoleCode = from.RoleCode; to.IsPrimary = from.IsPrimary; to.Status = from.Status;
        to.ValidFrom = from.ValidFrom; to.ValidTo = from.ValidTo; to.Notes = from.Notes;
        to.ReportsToContactId = from.ReportsToContactId; to.CrossCountryReason = from.CrossCountryReason;
        // AccountId / ContactId are immutable by design — a move is end + add, never a re-point.
    }

    // ---------------------------------------------------------------- plan/state types

    private sealed record ContactPlan(
        ParsedRow Row,
        ImportRowResultDto Result,
        DomainContact? NewContact = null,
        DomainRef? NewReference = null,
        DomainContact? ExistingContact = null,
        DomainContact? Draft = null);

    private sealed record LinkPlan(
        ParsedRow Row,
        ImportRowResultDto Result,
        DomainLink? NewLink = null,
        DomainLink? ExistingLink = null,
        DomainLink? LinkDraft = null);

    private sealed record LinkTargets(
        DomainContact? Contact, DomainAccount? Account, bool DependsOnPlannedContact,
        string? Error, string Status, string? Code)
    {
        public static LinkTargets Failure(string status, string code, string message)
            => new(null, null, false, message, status, code);
    }

    private sealed record LinkMatch(DomainLink? Link, string? Error, string? Code)
    {
        public static LinkMatch Failure(string code, string message) => new(null, message, code);
    }

    /// <summary>Cross-row state: contacts planned by this file, and the uniqueness reservations they imply.</summary>
    private sealed class ImportState
    {
        private readonly Dictionary<Guid, DomainContact> _plannedById = new();
        private readonly Dictionary<(string, string), DomainContact> _plannedByExternal = new();

        public HashSet<(string, string)> PlannedExternalRefs { get; } = new();
        public HashSet<Guid> UpdatedContactIds { get; } = new();
        public HashSet<(Guid, Guid, string)> PlannedActiveLinks { get; } = new();
        public HashSet<(Guid, string)> PlannedPrimaries { get; } = new();

        /// <summary>External identities whose Contacts row failed. A link that points at one of them is reported as a
        /// dependency skip, so the user fixes the contact row instead of chasing a misleading "contact not found".</summary>
        public HashSet<(string, string)> FailedContactKeys { get; } = new();

        public void RegisterFailedContact(string? sourceSystem, string? externalId)
        {
            if (!string.IsNullOrWhiteSpace(externalId))
            {
                var system = string.IsNullOrWhiteSpace(sourceSystem) ? DefaultSourceSystem : sourceSystem!.Trim();
                FailedContactKeys.Add((system, externalId!));
            }
        }

        public void RegisterPlannedContact(DomainContact contact, string? sourceSystem, string? externalId)
        {
            _plannedById[contact.Id] = contact;
            if (!string.IsNullOrWhiteSpace(externalId))
            {
                var system = string.IsNullOrWhiteSpace(sourceSystem) ? DefaultSourceSystem : sourceSystem!.Trim();
                _plannedByExternal[(system, externalId!)] = contact;
            }
        }

        public DomainContact? FindPlanned(Guid id) => _plannedById.GetValueOrDefault(id);

        public DomainContact? FindPlannedByExternal((string, string) key) => _plannedByExternal.GetValueOrDefault(key);
    }
}
