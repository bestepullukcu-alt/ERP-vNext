using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>SCMM-11 (CAND-CAP-0011) shared policy rules: structural validation, condition mapping, and the freeze
/// comparison. Self-contained (no cross-feature dependency); condition values are never checked against a hardcoded
/// vocabulary (sector-neutral).</summary>
internal static class EligibilityPolicyRules
{
    public static string? ValidateConditions(IReadOnlyList<EligibilityConditionInput>? conditions)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return null; // a policy with no conditions is valid (no restriction — deterministic Eligible)
        }

        foreach (var condition in conditions)
        {
            if (string.IsNullOrWhiteSpace(condition.Dimension))
            {
                return "Each eligibility condition requires a non-empty Dimension.";
            }

            if (!string.IsNullOrWhiteSpace(condition.Match) && !EligibilityMatchKinds.IsValid(condition.Match))
            {
                return $"Match must be one of: {string.Join(", ", EligibilityMatchKinds.All)}.";
            }

            var values = CleanValues(condition.Values);
            if (values.Count == 0)
            {
                return $"Eligibility condition '{condition.Dimension.Trim()}' must carry at least one value.";
            }
        }

        return null;
    }

    public static List<EligibilityCondition> ToDomain(IReadOnlyList<EligibilityConditionInput>? conditions)
        => (conditions ?? Array.Empty<EligibilityConditionInput>()).Select(c => new EligibilityCondition
        {
            Dimension = c.Dimension.Trim(),
            Match = EligibilityMatchKinds.Normalize(c.Match),
            Values = CleanValues(c.Values),
            Required = c.Required
        }).ToList();

    public static bool ConditionsEqual(List<EligibilityCondition> a, List<EligibilityCondition> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i].Dimension, b[i].Dimension, StringComparison.Ordinal)
                || !string.Equals(a[i].Match, b[i].Match, StringComparison.Ordinal)
                || a[i].Required != b[i].Required
                || !a[i].Values.SequenceEqual(b[i].Values))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> CleanValues(IReadOnlyList<string>? values)
        => (values ?? Array.Empty<string>()).Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0).ToList();
}

public sealed class CreateEligibilityPolicyHandler : IRequestHandler<CreateEligibilityPolicyCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IEligibilityPolicyRepository _policies;

    public CreateEligibilityPolicyHandler(
        ITenantContext tenant, IActorContext actor, IEligibilityPolicyRepository policies)
    {
        _tenant = tenant;
        _actor = actor;
        _policies = policies;
    }

    public async Task<Response<Guid>> Handle(CreateEligibilityPolicyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.PolicyCode))
        {
            return Response<Guid>.Fail("PolicyCode is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.PolicyName))
        {
            return Response<Guid>.Fail("PolicyName is required.", 400);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !EligibilityPolicyStatuses.IsValid(request.Status))
        {
            return Response<Guid>.Fail(
                $"Status must be one of: {string.Join(", ", EligibilityPolicyStatuses.All)}.", 400);
        }

        if (request.EffectiveTo is { } to && to < request.EffectiveFrom)
        {
            return Response<Guid>.Fail("EffectiveTo cannot be before EffectiveFrom.", 400);
        }

        var conditionError = EligibilityPolicyRules.ValidateConditions(request.Conditions);
        if (conditionError is not null)
        {
            return Response<Guid>.Fail(conditionError, 400);
        }

        var code = request.PolicyCode.Trim();
        if (await _policies.GetActiveByCodeAsync(tenantId, code, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived eligibility policy already uses PolicyCode '{code}' (eligibilityPolicyId={duplicate.Id}).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new EligibilityPolicy
        {
            TenantId = tenantId,
            PolicyCode = code,
            PolicyName = request.PolicyName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PolicyVersion = string.IsNullOrWhiteSpace(request.PolicyVersion) ? "1.0" : request.PolicyVersion.Trim(),
            Status = EligibilityPolicyStatuses.Normalize(request.Status),
            Conditions = EligibilityPolicyRules.ToDomain(request.Conditions),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _policies.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateEligibilityPolicyHandler : IRequestHandler<UpdateEligibilityPolicyCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IEligibilityPolicyRepository _policies;

    public UpdateEligibilityPolicyHandler(
        ITenantContext tenant, IActorContext actor, IEligibilityPolicyRepository policies)
    {
        _tenant = tenant;
        _actor = actor;
        _policies = policies;
    }

    public async Task<Response<bool>> Handle(UpdateEligibilityPolicyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _policies.GetByIdAsync(tenantId, request.EligibilityPolicyId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Eligibility policy not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Fail("An archived eligibility policy cannot be updated.", 409);
        }

        if (string.Equals(request.Status?.Trim(), EligibilityPolicyStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive an eligibility policy.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.PolicyName))
        {
            return Response<bool>.Fail("PolicyName is required.", 400);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !EligibilityPolicyStatuses.IsValid(request.Status))
        {
            return Response<bool>.Fail(
                $"Status must be one of: {string.Join(", ", EligibilityPolicyStatuses.All)}.", 400);
        }

        if (request.EffectiveTo is { } to && to < request.EffectiveFrom)
        {
            return Response<bool>.Fail("EffectiveTo cannot be before EffectiveFrom.", 400);
        }

        var conditionError = EligibilityPolicyRules.ValidateConditions(request.Conditions);
        if (conditionError is not null)
        {
            return Response<bool>.Fail(conditionError, 400);
        }

        var newConditions = EligibilityPolicyRules.ToDomain(request.Conditions);
        var conditionsChanged = !EligibilityPolicyRules.ConditionsEqual(entity.Conditions, newConditions);

        // A published version freezes its conditions — changing them needs a new version.
        if (entity.IsPublished() && conditionsChanged)
        {
            return Response<bool>.Fail(
                "Conditions are frozen on a published eligibility policy; create a new version to change them.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        entity.PolicyName = request.PolicyName.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Conditions = newConditions;
        entity.Status = EligibilityPolicyStatuses.Normalize(request.Status ?? entity.Status);
        if (!string.IsNullOrWhiteSpace(request.PolicyVersion))
        {
            entity.PolicyVersion = request.PolicyVersion.Trim();
        }

        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _policies.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveEligibilityPolicyHandler : IRequestHandler<ArchiveEligibilityPolicyCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IEligibilityPolicyRepository _policies;

    public ArchiveEligibilityPolicyHandler(
        ITenantContext tenant, IActorContext actor, IEligibilityPolicyRepository policies)
    {
        _tenant = tenant;
        _actor = actor;
        _policies = policies;
    }

    public async Task<Response<bool>> Handle(ArchiveEligibilityPolicyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _policies.GetByIdAsync(tenantId, request.EligibilityPolicyId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Eligibility policy not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = EligibilityPolicyStatuses.Archived;
        entity.ArchivedAt = now;
        entity.ArchivedBy = _actor.ActorName;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _policies.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}
