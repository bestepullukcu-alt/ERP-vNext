using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using AudienceProfileEntity = Diten.CrmService.Domain.Entities.AudienceProfile;

namespace Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Handlers;

/// <summary>SCMM-11 (AUD, RM3) — shared multi-axis + subject-scope rules for the audience-profile write handlers. The
/// axis vocabulary is NEVER validated against a hardcoded set (sector-neutral); only structural rules apply. No
/// eligibility/membership is computed anywhere (D8).</summary>
internal static class AudienceProfileRules
{
    /// <summary>Structural validation of the optional dimension list (400 message or null): non-empty axis code, at
    /// least one non-blank value per axis, and no duplicate axis.</summary>
    public static string? ValidateDimensions(IReadOnlyList<AudienceDimensionAssignmentInput>? dimensions)
    {
        if (dimensions is null || dimensions.Count == 0)
        {
            return null;
        }

        var axes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dimension in dimensions)
        {
            var axis = dimension.AxisCode?.Trim() ?? string.Empty;
            if (axis.Length == 0)
            {
                return "Each audience dimension requires a non-empty AxisCode.";
            }

            if (!axes.Add(axis))
            {
                return $"Audience dimension axis '{axis}' is assigned more than once.";
            }

            var values = CleanValues(dimension.Values);
            if (values.Count == 0)
            {
                return $"Audience dimension axis '{axis}' must carry at least one value.";
            }
        }

        return null;
    }

    public static List<AudienceDimensionAssignment> ToDomain(IReadOnlyList<AudienceDimensionAssignmentInput>? dimensions)
        => (dimensions ?? Array.Empty<AudienceDimensionAssignmentInput>()).Select(d => new AudienceDimensionAssignment
        {
            AxisCode = d.AxisCode.Trim(),
            Values = CleanValues(d.Values)
        }).ToList();

    /// <summary>Subject-scope guard: a supplied <c>SubjectId</c> must reference an existing, non-archived subject.
    /// Null / empty is allowed — the profile stays tenant-global (backward compatible).</summary>
    public static async Task<string?> ValidateSubjectAsync(
        ISubjectRepository subjects, Guid tenantId, Guid? subjectId, CancellationToken cancellationToken)
    {
        if (subjectId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var subject = await subjects.GetByIdAsync(tenantId, id, cancellationToken);
        if (subject is null)
        {
            return "SubjectId does not reference an existing subject.";
        }

        return subject.IsArchived() ? "SubjectId cannot reference an archived subject." : null;
    }

    public static Guid? NormalizeSubject(Guid? subjectId) => subjectId is { } id && id != Guid.Empty ? id : null;

    private static List<string> CleanValues(IReadOnlyList<string>? values)
        => (values ?? Array.Empty<string>()).Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0).ToList();
}

public sealed class CreateAudienceProfileHandler : IRequestHandler<CreateAudienceProfileCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IAudienceProfileRepository _repository;
    private readonly ISubjectRepository _subjects;

    public CreateAudienceProfileHandler(
        ITenantContext tenant, IActorContext actor, IAudienceProfileRepository repository, ISubjectRepository subjects)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _subjects = subjects;
    }

    public async Task<Response<Guid>> Handle(CreateAudienceProfileCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = KnowledgeValidation.ValidateCode(request.ProfileCode, "ProfileCode")
            ?? KnowledgeValidation.ValidateName(request.ProfileName, "ProfileName")
            ?? KnowledgeValidation.ValidateProfileType(request.ProfileType)
            ?? KnowledgeValidation.ValidateTaxonomyStatus(request.Status)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? AudienceProfileRules.ValidateDimensions(request.Dimensions);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        var (refError, isConflict) = KnowledgeValidation.ValidateExternalReferences(request.ExternalReferences);
        if (refError is not null)
        {
            return Response<Guid>.Fail(refError, isConflict ? 409 : 400);
        }

        // SCMM-11 (AUD) — subject-scope guard (only when a SubjectId is supplied; null = tenant-global, back-compat).
        var subjectError = await AudienceProfileRules.ValidateSubjectAsync(
            _subjects, tenantId, request.SubjectId, cancellationToken);
        if (subjectError is not null)
        {
            return Response<Guid>.Fail(subjectError, 400);
        }

        var code = request.ProfileCode.Trim();
        if (await _repository.GetActiveByCodeAsync(tenantId, code, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived profile already uses ProfileCode '{code}' (audienceProfileId={duplicate.Id}).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var profile = new AudienceProfileEntity
        {
            TenantId = tenantId,
            ProfileCode = code,
            ProfileName = request.ProfileName.Trim(),
            Description = KnowledgeValidation.Trim(request.Description),
            SubjectId = AudienceProfileRules.NormalizeSubject(request.SubjectId),
            ProfileType = string.IsNullOrWhiteSpace(request.ProfileType)
                ? null
                : AudienceProfileTypes.Normalize(request.ProfileType),
            Dimensions = AudienceProfileRules.ToDomain(request.Dimensions),
            Status = TaxonomyStatuses.Normalize(request.Status),
            SortOrder = request.SortOrder,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Alias = KnowledgeMapper.CleanAlias(request.Alias),
            ExternalReferences = KnowledgeMapper.ToEntities(request.ExternalReferences, now),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _repository.InsertAsync(profile, cancellationToken);
        return Response<Guid>.Success(profile.Id, 201);
    }
}

public sealed class UpdateAudienceProfileHandler : IRequestHandler<UpdateAudienceProfileCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IAudienceProfileRepository _repository;
    private readonly ISubjectRepository _subjects;

    public UpdateAudienceProfileHandler(
        ITenantContext tenant, IActorContext actor, IAudienceProfileRepository repository, ISubjectRepository subjects)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _subjects = subjects;
    }

    public async Task<Response<bool>> Handle(UpdateAudienceProfileCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var profile = await _repository.GetByIdAsync(tenantId, request.AudienceProfileId, cancellationToken);
        if (profile is null)
        {
            return Response<bool>.Fail("Audience profile not found.", 404);
        }

        if (profile.IsArchived())
        {
            return Response<bool>.Fail("An archived audience profile cannot be updated.", 409);
        }

        if (string.Equals(request.Status?.Trim(), TaxonomyStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive an audience profile.", 400);
        }

        var error = KnowledgeValidation.ValidateName(request.ProfileName, "ProfileName")
            ?? KnowledgeValidation.ValidateProfileType(request.ProfileType)
            ?? KnowledgeValidation.ValidateTaxonomyStatus(request.Status)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? AudienceProfileRules.ValidateDimensions(request.Dimensions);
        if (error is not null)
        {
            return Response<bool>.Fail(error, 400);
        }

        var (refError, isConflict) = KnowledgeValidation.ValidateExternalReferences(request.ExternalReferences);
        if (refError is not null)
        {
            return Response<bool>.Fail(refError, isConflict ? 409 : 400);
        }

        var subjectError = await AudienceProfileRules.ValidateSubjectAsync(
            _subjects, tenantId, request.SubjectId, cancellationToken);
        if (subjectError is not null)
        {
            return Response<bool>.Fail(subjectError, 400);
        }

        var now = DateTimeOffset.UtcNow;
        profile.ProfileName = request.ProfileName.Trim();
        profile.Description = KnowledgeValidation.Trim(request.Description);
        profile.SubjectId = AudienceProfileRules.NormalizeSubject(request.SubjectId);
        profile.ProfileType = string.IsNullOrWhiteSpace(request.ProfileType)
            ? null
            : AudienceProfileTypes.Normalize(request.ProfileType);
        profile.Dimensions = AudienceProfileRules.ToDomain(request.Dimensions);
        profile.Status = TaxonomyStatuses.Normalize(request.Status ?? profile.Status);
        profile.SortOrder = request.SortOrder;
        profile.EffectiveFrom = request.EffectiveFrom;
        profile.EffectiveTo = request.EffectiveTo;
        profile.Alias = KnowledgeMapper.CleanAlias(request.Alias);
        profile.ExternalReferences = KnowledgeMapper.ToEntities(request.ExternalReferences, now);
        profile.UpdatedAt = now;
        profile.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(profile, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveAudienceProfileHandler : IRequestHandler<ArchiveAudienceProfileCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IAudienceProfileRepository _repository;

    public ArchiveAudienceProfileHandler(
        ITenantContext tenant, IActorContext actor, IAudienceProfileRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(
        ArchiveAudienceProfileCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var profile = await _repository.GetByIdAsync(tenantId, request.AudienceProfileId, cancellationToken);
        if (profile is null)
        {
            return Response<bool>.Fail("Audience profile not found.", 404);
        }

        if (profile.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        profile.Status = TaxonomyStatuses.Archived;
        profile.ArchivedAt = now;
        profile.ArchivedBy = _actor.ActorName;
        profile.UpdatedAt = now;
        profile.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(profile, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class UnarchiveAudienceProfileHandler
    : IRequestHandler<UnarchiveAudienceProfileCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IAudienceProfileRepository _repository;

    public UnarchiveAudienceProfileHandler(
        ITenantContext tenant, IActorContext actor, IAudienceProfileRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(
        UnarchiveAudienceProfileCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var profile = await _repository.GetByIdAsync(tenantId, request.AudienceProfileId, cancellationToken);
        if (profile is null)
        {
            return Response<bool>.Fail("Audience profile not found.", 404);
        }

        if (!profile.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        // ProfileCode is unique only among non-archived rows, so it may have been reused while this one was archived.
        if (await _repository.GetActiveByCodeAsync(tenantId, profile.ProfileCode, cancellationToken) is { } holder)
        {
            return Response<bool>.Fail(
                $"ProfileCode '{profile.ProfileCode}' is now used by a non-archived audience profile "
                + $"(audienceProfileId={holder.Id}).", 409);
        }

        // Comes back as inactive, never straight to active.
        var now = DateTimeOffset.UtcNow;
        profile.Status = TaxonomyStatuses.Inactive;
        profile.ArchivedAt = null;
        profile.ArchivedBy = null;
        profile.UpdatedAt = now;
        profile.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(profile, cancellationToken);
        return Response<bool>.Success(true);
    }
}
