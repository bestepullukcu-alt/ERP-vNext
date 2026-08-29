using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Handlers;

/// <summary>Shared FU03 write-path validation. TenantId is always the claim-resolved value; the frequency vocabulary
/// is validated in-domain (structural). Nothing here deletes a policy.</summary>
internal static class VisitFrequencyPolicyWrite
{
    /// <summary>Runs every structural rule shared by create and update. Returns an error message or null.</summary>
    public static string? Validate(
        string frequencyType, int requiredVisitCount, string periodType, DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo, int priority, string source, string? status,
        Guid? campaignId, Guid? segmentId, Guid? cycleId, Guid? cyclePeriodId, string? notes)
    {
        return VisitFrequencyPolicyValidation.ValidateFrequencyType(frequencyType)
            ?? VisitFrequencyPolicyValidation.ValidatePeriodType(periodType)
            ?? VisitFrequencyPolicyValidation.ValidateSource(source)
            ?? VisitFrequencyPolicyValidation.ValidateStatusValue(status)
            ?? VisitFrequencyPolicyValidation.ValidateRequiredVisitCount(requiredVisitCount)
            ?? VisitFrequencyPolicyValidation.ValidatePriority(priority)
            ?? VisitFrequencyPolicyValidation.ValidateEffectiveRange(effectiveFrom, effectiveTo)
            ?? VisitFrequencyPolicyValidation.ValidateFrequencyPeriodCombination(frequencyType, periodType)
            ?? VisitFrequencyPolicyValidation.ValidateCycleContext(frequencyType, periodType, cycleId, cyclePeriodId)
            ?? VisitFrequencyPolicyValidation.ValidateCampaignContext(periodType, source, campaignId)
            ?? VisitFrequencyPolicyValidation.ValidateSegmentContext(source, segmentId)
            ?? VisitFrequencyPolicyValidation.ValidateCustom(frequencyType, notes);
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreateVisitFrequencyPolicyHandler : IRequestHandler<CreateVisitFrequencyPolicyCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitFrequencyPolicyRepository _repository;

    public CreateVisitFrequencyPolicyHandler(
        ITenantContext tenant, IActorContext actor, IVisitFrequencyPolicyRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateVisitFrequencyPolicyCommand request, CancellationToken cancellationToken)
    {
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

        if (VisitFrequencyPolicyValidation.ValidateTargetType(request.TargetType) is { } targetTypeError)
        {
            return Response<Guid>.Fail(targetTypeError, 400);
        }

        if (VisitFrequencyPolicyValidation.ValidateTargetId(request.TargetId) is { } targetIdError)
        {
            return Response<Guid>.Fail(targetIdError, 400);
        }

        if (VisitFrequencyPolicyWrite.Validate(
                request.FrequencyType, request.RequiredVisitCount, request.PeriodType, request.EffectiveFrom,
                request.EffectiveTo, request.Priority, request.Source, request.Status,
                request.CampaignId, request.SegmentId, request.CycleId, request.CyclePeriodId, request.Notes) is { } error)
        {
            return Response<Guid>.Fail(error, 400);
        }

        var policyCode = request.PolicyCode.Trim();
        if (await _repository.GetActiveByCodeAsync(tenantId, policyCode, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived frequency policy already uses PolicyCode '{policyCode}' (policyId={duplicate.Id}). " +
                "PolicyCode must be unique among active policies.", 409);
        }

        var policy = new Vfp
        {
            TenantId = tenantId,
            PolicyCode = policyCode,
            PolicyName = request.PolicyName.Trim(),
            Description = VisitFrequencyPolicyWrite.Trim(request.Description),
            TargetType = FrequencyTargetType.Normalize(request.TargetType),
            TargetId = request.TargetId,
            BusinessUnit = VisitFrequencyPolicyWrite.Trim(request.BusinessUnit),
            TerritoryNodeId = request.TerritoryNodeId,
            CampaignId = request.CampaignId,
            SegmentId = request.SegmentId,
            BrandId = request.BrandId,
            ProductId = request.ProductId,
            CycleId = request.CycleId,
            CyclePeriodId = request.CyclePeriodId,
            FrequencyType = FrequencyType.Normalize(request.FrequencyType),
            RequiredVisitCount = request.RequiredVisitCount,
            PeriodType = FrequencyPeriodType.Normalize(request.PeriodType),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Priority = request.Priority,
            Source = FrequencySource.Normalize(request.Source),
            Status = FrequencyPolicyStatus.Normalize(request.Status),
            Notes = VisitFrequencyPolicyWrite.Trim(request.Notes),
            CreatedBy = _actor.ActorName
        };

        await _repository.InsertAsync(policy, cancellationToken);
        return Response<Guid>.Success(policy.Id, 201);
    }
}

public sealed class UpdateVisitFrequencyPolicyHandler : IRequestHandler<UpdateVisitFrequencyPolicyCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitFrequencyPolicyRepository _repository;

    public UpdateVisitFrequencyPolicyHandler(
        ITenantContext tenant, IActorContext actor, IVisitFrequencyPolicyRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(UpdateVisitFrequencyPolicyCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var policy = await _repository.GetByIdAsync(tenantId, request.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Response<bool>.Fail("Visit frequency policy not found.", 404);
        }

        if (string.Equals(policy.Status, FrequencyPolicyStatus.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("An archived policy cannot be updated. Archived policies are read-only history.", 409);
        }

        if (string.IsNullOrWhiteSpace(request.PolicyName))
        {
            return Response<bool>.Fail("PolicyName is required.", 400);
        }

        // Archiving is a dedicated endpoint (it stamps ArchivedAt/By); update never archives.
        if (string.Equals(request.Status?.Trim(), FrequencyPolicyStatus.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive a policy; update cannot set status=archived.", 400);
        }

        if (VisitFrequencyPolicyWrite.Validate(
                request.FrequencyType, request.RequiredVisitCount, request.PeriodType, request.EffectiveFrom,
                request.EffectiveTo, request.Priority, request.Source, request.Status,
                request.CampaignId, request.SegmentId, request.CycleId, request.CyclePeriodId, request.Notes) is { } error)
        {
            return Response<bool>.Fail(error, 400);
        }

        // PolicyCode and TargetType/TargetId are immutable — a new target is a new policy, not an edit of this one.
        policy.PolicyName = request.PolicyName.Trim();
        policy.Description = VisitFrequencyPolicyWrite.Trim(request.Description);
        policy.BusinessUnit = VisitFrequencyPolicyWrite.Trim(request.BusinessUnit);
        policy.TerritoryNodeId = request.TerritoryNodeId;
        policy.CampaignId = request.CampaignId;
        policy.SegmentId = request.SegmentId;
        policy.BrandId = request.BrandId;
        policy.ProductId = request.ProductId;
        policy.CycleId = request.CycleId;
        policy.CyclePeriodId = request.CyclePeriodId;
        policy.FrequencyType = FrequencyType.Normalize(request.FrequencyType);
        policy.RequiredVisitCount = request.RequiredVisitCount;
        policy.PeriodType = FrequencyPeriodType.Normalize(request.PeriodType);
        policy.EffectiveFrom = request.EffectiveFrom;
        policy.EffectiveTo = request.EffectiveTo;
        policy.Priority = request.Priority;
        policy.Source = FrequencySource.Normalize(request.Source);
        policy.Status = FrequencyPolicyStatus.Normalize(request.Status ?? policy.Status);
        policy.Notes = VisitFrequencyPolicyWrite.Trim(request.Notes);
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        policy.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(policy, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveVisitFrequencyPolicyHandler : IRequestHandler<ArchiveVisitFrequencyPolicyCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitFrequencyPolicyRepository _repository;

    public ArchiveVisitFrequencyPolicyHandler(
        ITenantContext tenant, IActorContext actor, IVisitFrequencyPolicyRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(ArchiveVisitFrequencyPolicyCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var policy = await _repository.GetByIdAsync(tenantId, request.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Response<bool>.Fail("Visit frequency policy not found.", 404);
        }

        if (string.Equals(policy.Status, FrequencyPolicyStatus.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Success(true); // idempotent
        }

        policy.Status = FrequencyPolicyStatus.Archived;
        policy.ArchivedAt = DateTimeOffset.UtcNow;
        policy.ArchivedBy = _actor.ActorName;
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        policy.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(policy, cancellationToken);
        return Response<bool>.Success(true);
    }
}
