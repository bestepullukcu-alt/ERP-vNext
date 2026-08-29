using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountRelationship.Commands;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainRel = Diten.CrmService.Domain.Entities.AccountRelationship;

namespace Diten.CrmService.Application.Features.AccountRelationship.Handlers;

internal static class RelationshipReferenceValidation
{
    public const string TypeSet = "account-relationship-type";
    public const string StatusSet = "account-relationship-status";

    public static async Task<string?> ValidateAsync(IReferenceDataValidator validator, string setCode, string value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"'{setCode}' is required.";
        }

        var r = await validator.ValidateAsync(setCode, value, ct);
        return r.Status switch
        {
            ReferenceValidationStatus.InvalidValue => $"'{value}' is not a valid published value of reference set '{setCode}'.",
            ReferenceValidationStatus.SetMissing => $"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).",
            _ => null
        };
    }

    /// <summary>ValidFrom must not be after ValidTo. Also guards the End action (EndDate/ValidTo before ValidFrom).</summary>
    public static string? ValidateValidity(DateTimeOffset? validFrom, DateTimeOffset? validTo)
        => validFrom is { } f && validTo is { } t && f > t
            ? "ValidFrom cannot be after ValidTo."
            : null;
}

public sealed class CreateAccountRelationshipHandler : IRequestHandler<CreateAccountRelationshipCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountRelationshipRepository _relationships;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IReferenceMetadataReader _metadataReader;
    private readonly IContactAuditPublisher _audit;

    public CreateAccountRelationshipHandler(
        ITenantContext tenant, IAccountRepository accounts, IAccountRelationshipRepository relationships,
        IReferenceDataValidator referenceValidator, IReferenceMetadataReader metadataReader, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _relationships = relationships;
        _referenceValidator = referenceValidator;
        _metadataReader = metadataReader;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateAccountRelationshipCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var sourceAccount = await _accounts.GetByIdAsync(tenantId, request.SourceAccountId, cancellationToken);
        if (sourceAccount is null)
        {
            return Response<Guid>.Fail("Source account not found.", 404);
        }

        var targetAccount = await _accounts.GetByIdAsync(tenantId, request.TargetAccountId, cancellationToken);
        if (targetAccount is null)
        {
            return Response<Guid>.Fail("Target account not found.", 404);
        }

        var typeError = await RelationshipReferenceValidation.ValidateAsync(_referenceValidator, RelationshipReferenceValidation.TypeSet, request.RelationshipType, cancellationToken);
        if (typeError is not null)
        {
            return Response<Guid>.Fail(typeError, 400);
        }

        var statusError = await RelationshipReferenceValidation.ValidateAsync(_referenceValidator, RelationshipReferenceValidation.StatusSet, request.Status, cancellationToken);
        if (statusError is not null)
        {
            return Response<Guid>.Fail(statusError, 400);
        }

        if (RelationshipReferenceValidation.ValidateValidity(request.ValidFrom, request.ValidTo) is { } validityError)
        {
            return Response<Guid>.Fail(validityError, 400);
        }

        var relationshipType = request.RelationshipType.Trim();
        var attributes = await _metadataReader.GetValueAttributesAsync(RelationshipReferenceValidation.TypeSet, relationshipType, cancellationToken);
        var metadata = RelationshipTypeMetadata.Parse(attributes);

        // Self-link forbidden unless the type's metadata explicitly allows it (D4).
        if (request.SourceAccountId == request.TargetAccountId && !metadata.SelfAllowed)
        {
            return Response<Guid>.Fail("A self-relationship is not allowed for this relationship type.", 400);
        }

        // MOD-0150 hardening: cross-country Account↔Account relationship is controlled — reason required when both
        // countries are known and differ (e.g. a TR pharmacy linked "nearby" to a US hospital). Missing country never
        // blocks; the reason text is never written to audit. First release is reason-required for all cross-country
        // types (conservative); a per-type location policy (block "nearby") is a follow-up needing new type metadata.
        var crossCountry = CrossCountryPolicy.Evaluate(sourceAccount.CountryRef, targetAccount.CountryRef, request.CrossCountryReason);
        if (crossCountry.ReasonRequiredButMissing)
        {
            return Response<Guid>.Fail(
                "These accounts are in different countries. Provide a business reason before creating this relationship.", 400);
        }

        // Duplicate: for bidirectional types the reverse pair counts as the same link (D5).
        if (await _relationships.ExistsActivePairAsync(
                tenantId, request.SourceAccountId, request.TargetAccountId, relationshipType, metadata.IsBidirectional, excludeId: null, cancellationToken))
        {
            return Response<Guid>.Fail("An active relationship of this type already exists between the accounts.", 409);
        }

        var relationship = new DomainRel
        {
            TenantId = tenantId,
            SourceAccountId = request.SourceAccountId,
            TargetAccountId = request.TargetAccountId,
            RelationshipType = relationshipType,
            Direction = metadata.DirectionCode,
            Status = request.Status.Trim(),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Notes = request.Notes?.Trim(),
            CrossCountryReason = crossCountry.IsCrossCountry ? request.CrossCountryReason?.Trim() : null
        };

        await _relationships.InsertAsync(relationship, cancellationToken);
        // Audit carries the target id + non-PII cross-country descriptor (country codes only), never the reason text.
        await _audit.PublishAsync("account-relationship.create", tenantId, relationship.SourceAccountId,
            $"target={relationship.TargetAccountId} {CrossCountryPolicy.AuditNote(crossCountry)}", cancellationToken);
        return Response<Guid>.Success(relationship.Id, 201);
    }
}

public sealed class UpdateAccountRelationshipHandler : IRequestHandler<UpdateAccountRelationshipCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRelationshipRepository _relationships;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IReferenceMetadataReader _metadataReader;
    private readonly IContactAuditPublisher _audit;

    public UpdateAccountRelationshipHandler(
        ITenantContext tenant, IAccountRelationshipRepository relationships,
        IReferenceDataValidator referenceValidator, IReferenceMetadataReader metadataReader, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _relationships = relationships;
        _referenceValidator = referenceValidator;
        _metadataReader = metadataReader;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpdateAccountRelationshipCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var relationship = await _relationships.GetByIdAsync(tenantId, request.RelationshipId, cancellationToken);
        if (relationship is null || relationship.SourceAccountId != request.SourceAccountId)
        {
            return Response<bool>.Fail("Account relationship not found.", 404);
        }

        var typeError = await RelationshipReferenceValidation.ValidateAsync(_referenceValidator, RelationshipReferenceValidation.TypeSet, request.RelationshipType, cancellationToken);
        if (typeError is not null)
        {
            return Response<bool>.Fail(typeError, 400);
        }

        var statusError = await RelationshipReferenceValidation.ValidateAsync(_referenceValidator, RelationshipReferenceValidation.StatusSet, request.Status, cancellationToken);
        if (statusError is not null)
        {
            return Response<bool>.Fail(statusError, 400);
        }

        if (RelationshipReferenceValidation.ValidateValidity(request.ValidFrom, request.ValidTo) is { } validityError)
        {
            return Response<bool>.Fail(validityError, 400);
        }

        var relationshipType = request.RelationshipType.Trim();
        var attributes = await _metadataReader.GetValueAttributesAsync(RelationshipReferenceValidation.TypeSet, relationshipType, cancellationToken);
        var metadata = RelationshipTypeMetadata.Parse(attributes);

        // A type change must not collide with another active relationship (bidirectional-aware).
        if (await _relationships.ExistsActivePairAsync(
                tenantId, relationship.SourceAccountId, relationship.TargetAccountId, relationshipType, metadata.IsBidirectional, excludeId: relationship.Id, cancellationToken))
        {
            return Response<bool>.Fail("An active relationship of this type already exists between the accounts.", 409);
        }

        relationship.RelationshipType = relationshipType;
        relationship.Direction = metadata.DirectionCode;
        relationship.Status = request.Status.Trim();
        relationship.ValidFrom = request.ValidFrom;
        relationship.ValidTo = request.ValidTo;
        relationship.Notes = request.Notes?.Trim();
        // The relationship's accounts (and thus its cross-country status) never change on update; carry the reason forward.
        if (!string.IsNullOrWhiteSpace(request.CrossCountryReason))
        {
            relationship.CrossCountryReason = request.CrossCountryReason.Trim();
        }
        relationship.UpdatedAt = DateTimeOffset.UtcNow;

        await _relationships.UpdateAsync(relationship, cancellationToken);
        await _audit.PublishAsync("account-relationship.update", tenantId, relationship.SourceAccountId, relationship.TargetAccountId.ToString(), cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class DeleteAccountRelationshipHandler : IRequestHandler<DeleteAccountRelationshipCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRelationshipRepository _relationships;
    private readonly IContactAuditPublisher _audit;

    public DeleteAccountRelationshipHandler(ITenantContext tenant, IAccountRelationshipRepository relationships, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _relationships = relationships;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(DeleteAccountRelationshipCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var relationship = await _relationships.GetByIdAsync(tenantId, request.RelationshipId, cancellationToken);
        if (relationship is null || relationship.SourceAccountId != request.SourceAccountId)
        {
            return Response<bool>.Fail("Account relationship not found.", 404);
        }

        relationship.IsDeleted = true;
        relationship.DeletedAt = DateTimeOffset.UtcNow;
        relationship.UpdatedAt = DateTimeOffset.UtcNow;
        await _relationships.UpdateAsync(relationship, cancellationToken);

        await _audit.PublishAsync("account-relationship.delete", tenantId, relationship.SourceAccountId, relationship.TargetAccountId.ToString(), cancellationToken);
        return Response<bool>.Success(true);
    }
}
