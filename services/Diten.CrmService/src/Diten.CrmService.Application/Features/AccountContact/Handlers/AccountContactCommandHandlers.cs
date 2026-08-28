using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountContact.Commands;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainLink = Diten.CrmService.Domain.Entities.AccountContactLink;

namespace Diten.CrmService.Application.Features.AccountContact.Handlers;

/// <summary>Shared MOD-0048 role validation + existence checks for AccountContactLink command handlers.</summary>
internal static class AccountContactValidation
{
    public const string ContactRoleSet = "contact-role";

    public static async Task<string?> ValidateRoleAsync(IReferenceDataValidator validator, string roleCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return $"'{ContactRoleSet}' is required.";
        }

        var result = await validator.ValidateAsync(ContactRoleSet, roleCode, ct);
        return result.Status switch
        {
            ReferenceValidationStatus.InvalidValue => $"'{roleCode}' is not a valid published value of reference set '{ContactRoleSet}'.",
            ReferenceValidationStatus.SetMissing => $"Required reference set '{ContactRoleSet}' is not published yet (MOD-0048 authoring pending).",
            _ => null
        };
    }

    /// <summary>ValidFrom must not be after ValidTo. Also guards the End action (ValidTo/EndDate before ValidFrom).</summary>
    public static string? ValidateValidity(DateTimeOffset? validFrom, DateTimeOffset? validTo)
        => validFrom is { } f && validTo is { } t && f > t
            ? "ValidFrom cannot be after ValidTo."
            : null;

    /// <summary>
    /// MOD-0150 in-account hierarchy validation. A contact's "reports to" (manager) within an account must: not be
    /// itself; be a contact that has an active link to the SAME account; and not create a reporting cycle. Uses the
    /// account's active links (no new repository method). Returns an error message, or null when valid/unset.
    /// </summary>
    public static async Task<string?> ValidateReportsToAsync(
        IAccountContactLinkRepository links, Guid tenantId, Guid accountId, Guid contactId,
        Guid? reportsToContactId, Guid? excludeLinkId, CancellationToken ct)
    {
        if (reportsToContactId is not { } parentId)
        {
            return null; // optional — no manager set
        }

        if (parentId == contactId)
        {
            return "A contact cannot report to itself.";
        }

        var active = (await links.ListByAccountAsync(tenantId, accountId, ct))
            .Where(l => !Diten.CrmService.Domain.Entities.RelationshipLifecycle.IsClosed(l.Status))
            .ToList();

        if (!active.Any(l => l.ContactId == parentId))
        {
            return "The manager must be a contact linked to this account.";
        }

        // Build the contact→manager map from existing links (excluding the link being edited), add the proposed edge,
        // then walk upward from the contact; reaching the contact again means the edge would create a cycle.
        var map = new Dictionary<Guid, Guid>();
        foreach (var l in active)
        {
            if (excludeLinkId is { } ex && l.Id == ex)
            {
                continue;
            }
            if (l.ReportsToContactId is { } rt)
            {
                map[l.ContactId] = rt;
            }
        }
        map[contactId] = parentId;

        var visited = new HashSet<Guid>();
        var current = contactId;
        while (map.TryGetValue(current, out var next))
        {
            if (next == contactId)
            {
                return "This would create a reporting cycle within the account.";
            }
            if (!visited.Add(next))
            {
                break; // pre-existing loop elsewhere — stop walking (defensive)
            }
            current = next;
        }

        return null;
    }
}

public sealed class LinkContactToAccountHandler : IRequestHandler<LinkContactToAccountCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contacts;
    private readonly IAccountContactLinkRepository _links;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public LinkContactToAccountHandler(
        ITenantContext tenant, IAccountRepository accounts, IContactRepository contacts,
        IAccountContactLinkRepository links, IReferenceDataValidator referenceValidator, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _contacts = contacts;
        _links = links;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(LinkContactToAccountCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        // Missing OR soft-deleted account/contact both surface as 404 (the repos filter IsDeleted; a link may not
        // reference a non-active account/contact). Decision recorded in the FU03 audit.
        var account = await _accounts.GetByIdAsync(tenantId, request.AccountId, cancellationToken);
        if (account is null)
        {
            return Response<Guid>.Fail("Account not found.", 404);
        }

        var contact = await _contacts.GetByIdAsync(tenantId, request.ContactId, cancellationToken);
        if (contact is null)
        {
            return Response<Guid>.Fail("Contact not found.", 404);
        }

        var roleError = await AccountContactValidation.ValidateRoleAsync(_referenceValidator, request.RoleCode, cancellationToken);
        if (roleError is not null)
        {
            return Response<Guid>.Fail(roleError, 400);
        }

        if (AccountContactValidation.ValidateValidity(request.ValidFrom, request.ValidTo) is { } validityError)
        {
            return Response<Guid>.Fail(validityError, 400);
        }

        // MOD-0150 hardening: cross-country Contact↔Account link is controlled — reason required when both countries are
        // known and differ. Missing country never blocks. Never silent, never PII in audit.
        var crossCountry = CrossCountryPolicy.Evaluate(contact.CountryRef, account.CountryRef, request.CrossCountryReason);
        if (crossCountry.ReasonRequiredButMissing)
        {
            return Response<Guid>.Fail(
                "This contact and account are in different countries. Provide a business reason before linking.", 400);
        }

        var roleCode = request.RoleCode.Trim();

        if (await _links.ExistsActiveAsync(tenantId, request.AccountId, request.ContactId, roleCode, excludeId: null, cancellationToken))
        {
            return Response<Guid>.Fail("This contact is already linked to the account with this role.", 409);
        }

        if (request.IsPrimary
            && await _links.ExistsPrimaryAsync(tenantId, request.AccountId, roleCode, excludeId: null, cancellationToken))
        {
            return Response<Guid>.Fail("A primary contact already exists for this account and role.", 409);
        }

        if (await AccountContactValidation.ValidateReportsToAsync(
                _links, tenantId, request.AccountId, request.ContactId, request.ReportsToContactId, excludeLinkId: null, cancellationToken) is { } reportsToError)
        {
            return Response<Guid>.Fail(reportsToError, 400);
        }

        var link = new DomainLink
        {
            TenantId = tenantId,
            AccountId = request.AccountId,
            ContactId = request.ContactId,
            RoleCode = roleCode,
            IsPrimary = request.IsPrimary,
            Status = "active",
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Notes = request.Notes?.Trim(),
            CrossCountryReason = crossCountry.IsCrossCountry ? request.CrossCountryReason?.Trim() : null,
            ReportsToContactId = request.ReportsToContactId
        };

        await _links.InsertAsync(link, cancellationToken);
        // Audit carries the account id + non-PII cross-country descriptor (country codes only), never the reason text.
        await _audit.PublishAsync("account-contact.link", tenantId, link.ContactId,
            $"account={link.AccountId} {CrossCountryPolicy.AuditNote(crossCountry)}", cancellationToken);
        return Response<Guid>.Success(link.Id, 201);
    }
}

public sealed class UpdateAccountContactLinkHandler : IRequestHandler<UpdateAccountContactLinkCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountContactLinkRepository _links;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public UpdateAccountContactLinkHandler(
        ITenantContext tenant, IAccountContactLinkRepository links, IReferenceDataValidator referenceValidator, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _links = links;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpdateAccountContactLinkCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var link = await _links.GetByIdAsync(tenantId, request.LinkId, cancellationToken);
        if (link is null || link.AccountId != request.AccountId)
        {
            return Response<bool>.Fail("Account-contact link not found.", 404);
        }

        var roleError = await AccountContactValidation.ValidateRoleAsync(_referenceValidator, request.RoleCode, cancellationToken);
        if (roleError is not null)
        {
            return Response<bool>.Fail(roleError, 400);
        }

        if (AccountContactValidation.ValidateValidity(request.ValidFrom, request.ValidTo) is { } validityError)
        {
            return Response<bool>.Fail(validityError, 400);
        }

        var roleCode = request.RoleCode.Trim();

        // Role change must not collide with another active link. Ended/inactive links are excluded by the repo, so an
        // End (Status→ended) followed by a fresh link is allowed even for the same natural key (historical lifecycle).
        if (await _links.ExistsActiveAsync(tenantId, link.AccountId, link.ContactId, roleCode, excludeId: link.Id, cancellationToken))
        {
            return Response<bool>.Fail("This contact is already linked to the account with this role.", 409);
        }

        if (request.IsPrimary
            && await _links.ExistsPrimaryAsync(tenantId, link.AccountId, roleCode, excludeId: link.Id, cancellationToken))
        {
            return Response<bool>.Fail("A primary contact already exists for this account and role.", 409);
        }

        if (await AccountContactValidation.ValidateReportsToAsync(
                _links, tenantId, link.AccountId, link.ContactId, request.ReportsToContactId, excludeLinkId: link.Id, cancellationToken) is { } reportsToError)
        {
            return Response<bool>.Fail(reportsToError, 400);
        }

        link.RoleCode = roleCode;
        link.IsPrimary = request.IsPrimary;
        link.ValidFrom = request.ValidFrom;
        link.ValidTo = request.ValidTo;
        link.Notes = request.Notes?.Trim();
        link.ReportsToContactId = request.ReportsToContactId;
        // The link's account/contact (and thus its cross-country status) never change on update; carry the reason forward.
        if (!string.IsNullOrWhiteSpace(request.CrossCountryReason))
        {
            link.CrossCountryReason = request.CrossCountryReason.Trim();
        }
        // Historical lifecycle: an End action sends Status ("ended"/"inactive") + ValidTo; null keeps the current status.
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            link.Status = request.Status.Trim().ToLowerInvariant();
        }
        link.UpdatedAt = DateTimeOffset.UtcNow;

        await _links.UpdateAsync(link, cancellationToken);
        await _audit.PublishAsync("account-contact.update", tenantId, link.ContactId, link.AccountId.ToString(), cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class DeleteAccountContactLinkHandler : IRequestHandler<DeleteAccountContactLinkCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountContactLinkRepository _links;
    private readonly IContactAuditPublisher _audit;

    public DeleteAccountContactLinkHandler(ITenantContext tenant, IAccountContactLinkRepository links, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _links = links;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(DeleteAccountContactLinkCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var link = await _links.GetByIdAsync(tenantId, request.LinkId, cancellationToken);
        if (link is null || link.AccountId != request.AccountId)
        {
            return Response<bool>.Fail("Account-contact link not found.", 404);
        }

        link.IsDeleted = true;
        link.DeletedAt = DateTimeOffset.UtcNow;
        link.UpdatedAt = DateTimeOffset.UtcNow;
        await _links.UpdateAsync(link, cancellationToken);

        await _audit.PublishAsync("account-contact.unlink", tenantId, link.ContactId, link.AccountId.ToString(), cancellationToken);
        return Response<bool>.Success(true);
    }
}
