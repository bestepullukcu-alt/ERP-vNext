using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ConsentPreference.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using PrefType = Diten.CrmService.Domain.Entities.PreferenceType;

namespace Diten.CrmService.Application.Features.ConsentPreference.Handlers;

/// <summary>Shared FU02 preference write-path validation. A preference never grants: it is authored as a preference or
/// restriction, and the evaluator can only use it to restrict further.</summary>
internal static class PreferenceRecordWrite
{
    public static (string? Error, int StatusCode) Validate(
        string preferenceType,
        string preferenceValue,
        int priority,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        string source,
        IReadOnlyList<ConsentExternalReferenceInput>? externalReferences)
    {
        var error = ConsentPreferenceValidation.ValidatePreferenceType(preferenceType)
            ?? ConsentPreferenceValidation.ValidatePreferenceValue(preferenceType, preferenceValue)
            ?? ConsentPreferenceValidation.ValidatePriority(priority)
            ?? ConsentPreferenceValidation.ValidateSource(source)
            ?? ConsentPreferenceValidation.ValidateEffectiveFrom(effectiveFrom)
            ?? ConsentPreferenceValidation.ValidateEffectiveRange(effectiveFrom, effectiveTo);

        if (error is not null)
        {
            return (error, 400);
        }

        var (referenceError, isConflict) =
            ConsentPreferenceValidation.ValidateExternalReferences(externalReferences);
        return referenceError is null ? (null, 0) : (referenceError, isConflict ? 409 : 400);
    }

    /// <summary>Same no-silent-merge rule as consent: a duplicate legacy mapping is reported, never merged.</summary>
    public static async Task<string?> FindExternalMappingConflictAsync(
        IPreferenceRecordRepository repository,
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
                       $"owned by preference record {existing.Id}. Silent merge is not performed — resolve the " +
                       "duplicate mapping explicitly.";
            }
        }

        return null;
    }
}

public sealed class CreatePreferenceRecordHandler : IRequestHandler<CreatePreferenceRecordCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPreferenceRecordRepository _repository;

    public CreatePreferenceRecordHandler(
        ITenantContext tenant, IActorContext actor, IPreferenceRecordRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreatePreferenceRecordCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (ConsentPreferenceValidation.ValidateSubjectType(request.SubjectType) is { } subjectTypeError)
        {
            return Response<Guid>.Fail(subjectTypeError, 400);
        }

        if (ConsentPreferenceValidation.ValidateSubjectId(request.SubjectId) is { } subjectIdError)
        {
            return Response<Guid>.Fail(subjectIdError, 400);
        }

        if (ConsentPreferenceValidation.ValidatePreferenceChannel(request.Channel) is { } channelError)
        {
            return Response<Guid>.Fail(channelError, 400);
        }

        var (error, statusCode) = PreferenceRecordWrite.Validate(
            request.PreferenceType, request.PreferenceValue, request.Priority, request.EffectiveFrom,
            request.EffectiveTo, request.Source, request.ExternalReferences);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, statusCode);
        }

        if (await PreferenceRecordWrite.FindExternalMappingConflictAsync(
                _repository, tenantId, null, request.ExternalReferences, cancellationToken) is { } conflict)
        {
            return Response<Guid>.Fail(conflict, 409);
        }

        var now = DateTimeOffset.UtcNow;
        var record = new PreferenceRecord
        {
            TenantId = tenantId,
            SubjectType = ConsentSubjectType.Normalize(request.SubjectType),
            SubjectId = request.SubjectId,
            Channel = PreferenceChannel.Normalize(request.Channel),
            PreferenceType = PrefType.Normalize(request.PreferenceType),
            PreferenceValue = request.PreferenceValue.Trim(),
            Priority = request.Priority,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Source = ConsentSource.Normalize(request.Source),
            Notes = ConsentRecordWrite.Trim(request.Notes),
            ExternalReferences = ConsentPreferenceMapper.ToEntities(request.ExternalReferences, now),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _repository.InsertAsync(record, cancellationToken);
        return Response<Guid>.Success(record.Id, 201);
    }
}

public sealed class UpdatePreferenceRecordHandler : IRequestHandler<UpdatePreferenceRecordCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPreferenceRecordRepository _repository;

    public UpdatePreferenceRecordHandler(
        ITenantContext tenant, IActorContext actor, IPreferenceRecordRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(UpdatePreferenceRecordCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var record = await _repository.GetByIdAsync(tenantId, request.PreferenceId, cancellationToken);
        if (record is null)
        {
            return Response<bool>.Fail("Preference record not found.", 404);
        }

        if (record.IsArchived())
        {
            return Response<bool>.Fail(
                "An archived preference record cannot be updated. Archived records are read-only history.", 409);
        }

        // PreferenceType is immutable, so the stored type governs the value validation.
        var (error, statusCode) = PreferenceRecordWrite.Validate(
            record.PreferenceType, request.PreferenceValue, request.Priority, request.EffectiveFrom,
            request.EffectiveTo, request.Source, request.ExternalReferences);
        if (error is not null)
        {
            return Response<bool>.Fail(error, statusCode);
        }

        if (await PreferenceRecordWrite.FindExternalMappingConflictAsync(
                _repository, tenantId, record.Id, request.ExternalReferences, cancellationToken) is { } conflict)
        {
            return Response<bool>.Fail(conflict, 409);
        }

        // SubjectType/SubjectId, Channel and PreferenceType are IMMUTABLE — a different restriction is a new record.
        var now = DateTimeOffset.UtcNow;
        record.PreferenceValue = request.PreferenceValue.Trim();
        record.Priority = request.Priority;
        record.EffectiveFrom = request.EffectiveFrom;
        record.EffectiveTo = request.EffectiveTo;
        record.Source = ConsentSource.Normalize(request.Source);
        record.Notes = ConsentRecordWrite.Trim(request.Notes);
        record.ExternalReferences = ConsentPreferenceMapper.ToEntities(request.ExternalReferences, now);
        record.UpdatedAt = now;
        record.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(record, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchivePreferenceRecordHandler : IRequestHandler<ArchivePreferenceRecordCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPreferenceRecordRepository _repository;

    public ArchivePreferenceRecordHandler(
        ITenantContext tenant, IActorContext actor, IPreferenceRecordRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(
        ArchivePreferenceRecordCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var record = await _repository.GetByIdAsync(tenantId, request.PreferenceId, cancellationToken);
        if (record is null)
        {
            return Response<bool>.Fail("Preference record not found.", 404);
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
