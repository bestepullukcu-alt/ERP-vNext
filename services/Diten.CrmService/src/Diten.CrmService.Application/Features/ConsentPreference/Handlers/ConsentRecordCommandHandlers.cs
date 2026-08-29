using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ConsentPreference.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ConsentPreference.Handlers;

/// <summary>Shared FU02 consent write-path validation. TenantId is always the claim-resolved value; the consent
/// vocabulary is validated in-domain (structural). Nothing here deletes a record.</summary>
internal static class ConsentRecordWrite
{
    /// <summary>Rules shared by create and update. Returns (error, statusCode) or (null, 0).</summary>
    public static (string? Error, int StatusCode) Validate(
        string legalBasis,
        string consentStatus,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        string source,
        ConsentEvidenceRefInput? evidenceRef,
        string? withdrawalReason,
        IReadOnlyList<ConsentExternalReferenceInput>? externalReferences)
    {
        var error = ConsentPreferenceValidation.ValidateLegalBasis(legalBasis)
            ?? ConsentPreferenceValidation.ValidateConsentStatus(consentStatus)
            ?? ConsentPreferenceValidation.ValidateSource(source)
            ?? ConsentPreferenceValidation.ValidateEffectiveFrom(effectiveFrom)
            ?? ConsentPreferenceValidation.ValidateEffectiveRange(effectiveFrom, effectiveTo)
            ?? ConsentPreferenceValidation.ValidateWithdrawal(consentStatus, withdrawalReason)
            ?? ConsentPreferenceValidation.ValidateEvidenceRef(evidenceRef);

        if (error is not null)
        {
            return (error, 400);
        }

        var (referenceError, isConflict) =
            ConsentPreferenceValidation.ValidateExternalReferences(externalReferences);
        return referenceError is null ? (null, 0) : (referenceError, isConflict ? 409 : 400);
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Cross-record duplicate-mapping guard. A (SourceSystem, ExternalId) pair already owned by another non-archived
    /// record is a reported conflict — MOD-0164 never silently merges two legacy identities into one consent record,
    /// because that would fuse two different opt-in/opt-out histories.
    /// </summary>
    public static async Task<string?> FindExternalMappingConflictAsync(
        IConsentRecordRepository repository,
        Guid tenantId,
        Guid? selfId,
        IReadOnlyList<ConsentExternalReferenceInput>? references,
        CancellationToken cancellationToken)
    {
        if (references is null || references.Count == 0)
        {
            return null;
        }

        foreach (var reference in references)
        {
            var existing = await repository.FindByExternalReferenceAsync(
                tenantId, reference.SourceSystem.Trim(), reference.ExternalId.Trim(), cancellationToken);
            if (existing is not null && existing.Id != selfId)
            {
                return $"External mapping '{reference.SourceSystem.Trim()}/{reference.ExternalId.Trim()}' is already " +
                       $"owned by consent record {existing.Id}. Silent merge is not performed — resolve the duplicate " +
                       "mapping explicitly.";
            }
        }

        return null;
    }
}

public sealed class CreateConsentRecordHandler : IRequestHandler<CreateConsentRecordCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConsentRecordRepository _repository;

    public CreateConsentRecordHandler(ITenantContext tenant, IActorContext actor, IConsentRecordRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateConsentRecordCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        // The question dimensions first — they are immutable after create.
        if (ConsentPreferenceValidation.ValidateSubjectType(request.SubjectType) is { } subjectTypeError)
        {
            return Response<Guid>.Fail(subjectTypeError, 400);
        }

        if (ConsentPreferenceValidation.ValidateSubjectId(request.SubjectId) is { } subjectIdError)
        {
            return Response<Guid>.Fail(subjectIdError, 400);
        }

        if (ConsentPreferenceValidation.ValidateConsentChannel(request.Channel) is { } channelError)
        {
            return Response<Guid>.Fail(channelError, 400);
        }

        if (ConsentPreferenceValidation.ValidatePurpose(request.Purpose) is { } purposeError)
        {
            return Response<Guid>.Fail(purposeError, 400);
        }

        if (ConsentPreferenceValidation.ValidateScope(request.ScopeType, request.ScopeId) is { } scopeError)
        {
            return Response<Guid>.Fail(scopeError, 400);
        }

        var (error, statusCode) = ConsentRecordWrite.Validate(
            request.LegalBasis, request.ConsentStatus, request.EffectiveFrom, request.EffectiveTo, request.Source,
            request.EvidenceRef, request.WithdrawalReason, request.ExternalReferences);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, statusCode);
        }

        if (await ConsentRecordWrite.FindExternalMappingConflictAsync(
                _repository, tenantId, null, request.ExternalReferences, cancellationToken) is { } conflict)
        {
            return Response<Guid>.Fail(conflict, 409);
        }

        var now = DateTimeOffset.UtcNow;
        var record = new ConsentRecord
        {
            TenantId = tenantId,
            SubjectType = ConsentSubjectType.Normalize(request.SubjectType),
            SubjectId = request.SubjectId,
            ScopeType = string.IsNullOrWhiteSpace(request.ScopeType)
                ? null
                : ConsentScopeType.Normalize(request.ScopeType),
            ScopeId = request.ScopeId is { } scopeId && scopeId != Guid.Empty ? scopeId : null,
            Channel = ConsentChannel.Normalize(request.Channel),
            Purpose = ConsentPurpose.Normalize(request.Purpose),
            LegalBasis = ConsentLegalBasis.Normalize(request.LegalBasis),
            ConsentStatus = ConsentStatuses.Normalize(request.ConsentStatus),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Source = ConsentSource.Normalize(request.Source),
            EvidenceRef = ConsentPreferenceMapper.ToEntity(request.EvidenceRef),
            WithdrawalReason = ConsentRecordWrite.Trim(request.WithdrawalReason),
            Notes = ConsentRecordWrite.Trim(request.Notes),
            ExternalReferences = ConsentPreferenceMapper.ToEntities(request.ExternalReferences, now),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _repository.InsertAsync(record, cancellationToken);
        return Response<Guid>.Success(record.Id, 201);
    }
}

public sealed class UpdateConsentRecordHandler : IRequestHandler<UpdateConsentRecordCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConsentRecordRepository _repository;

    public UpdateConsentRecordHandler(ITenantContext tenant, IActorContext actor, IConsentRecordRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(UpdateConsentRecordCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var record = await _repository.GetByIdAsync(tenantId, request.ConsentId, cancellationToken);
        if (record is null)
        {
            return Response<bool>.Fail("Consent record not found.", 404);
        }

        if (record.IsArchived())
        {
            return Response<bool>.Fail(
                "An archived consent record cannot be updated. Archived records are read-only history.", 409);
        }

        var (error, statusCode) = ConsentRecordWrite.Validate(
            request.LegalBasis, request.ConsentStatus, request.EffectiveFrom, request.EffectiveTo, request.Source,
            request.EvidenceRef, request.WithdrawalReason, request.ExternalReferences);
        if (error is not null)
        {
            return Response<bool>.Fail(error, statusCode);
        }

        if (await ConsentRecordWrite.FindExternalMappingConflictAsync(
                _repository, tenantId, record.Id, request.ExternalReferences, cancellationToken) is { } conflict)
        {
            return Response<bool>.Fail(conflict, 409);
        }

        // SubjectType/SubjectId, Channel, Purpose, ScopeType/ScopeId are IMMUTABLE — a different question is a
        // different record, so a permission can never be silently repurposed. A status transition (e.g. granted →
        // withdrawn) is allowed and audit stamped; the record is never deleted or blanked.
        var now = DateTimeOffset.UtcNow;
        record.LegalBasis = ConsentLegalBasis.Normalize(request.LegalBasis);
        record.ConsentStatus = ConsentStatuses.Normalize(request.ConsentStatus);
        record.EffectiveFrom = request.EffectiveFrom;
        record.EffectiveTo = request.EffectiveTo;
        record.Source = ConsentSource.Normalize(request.Source);
        record.EvidenceRef = ConsentPreferenceMapper.ToEntity(request.EvidenceRef);
        record.WithdrawalReason = ConsentRecordWrite.Trim(request.WithdrawalReason);
        record.Notes = ConsentRecordWrite.Trim(request.Notes);
        record.ExternalReferences = ConsentPreferenceMapper.ToEntities(request.ExternalReferences, now);
        record.UpdatedAt = now;
        record.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(record, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveConsentRecordHandler : IRequestHandler<ArchiveConsentRecordCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConsentRecordRepository _repository;

    public ArchiveConsentRecordHandler(ITenantContext tenant, IActorContext actor, IConsentRecordRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(ArchiveConsentRecordCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var record = await _repository.GetByIdAsync(tenantId, request.ConsentId, cancellationToken);
        if (record is null)
        {
            return Response<bool>.Fail("Consent record not found.", 404);
        }

        if (record.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        record.ArchivedAt = now;
        record.ArchivedBy = _actor.ActorName;
        record.UpdatedAt = now;
        record.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(record, cancellationToken);
        return Response<bool>.Success(true);
    }
}
