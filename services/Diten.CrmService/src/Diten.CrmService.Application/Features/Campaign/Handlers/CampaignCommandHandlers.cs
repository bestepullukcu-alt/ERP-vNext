using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Campaign.Commands;
using Diten.CrmService.Application.Features.Campaign.Rules;
using Diten.CrmService.Application.Features.Campaign.Services;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Features.Campaign.Handlers;

/// <summary>Shared FU04 campaign write-path validation. TenantId is always the claim-resolved value; the campaign
/// vocabulary is validated in-domain (structural). Nothing here deletes a campaign.</summary>
internal static class CampaignWrite
{
    /// <summary>Rules shared by create and update. Returns (error, statusCode) or (null, 0).</summary>
    public static (string? Error, int StatusCode) Validate(
        string campaignName,
        string campaignType,
        string? campaignStatus,
        string? objectiveType,
        DateTimeOffset startDate,
        DateTimeOffset? endDate,
        string? defaultConsentChannel,
        string? defaultConsentPurpose,
        Guid? cyclePeriodId)
    {
        var error = CampaignValidation.ValidateCampaignName(campaignName)
            ?? CampaignValidation.ValidateCampaignType(campaignType)
            ?? CampaignValidation.ValidateCampaignStatus(campaignStatus)
            ?? CampaignValidation.ValidateObjectiveType(objectiveType)
            ?? CampaignValidation.ValidateStartDate(startDate)
            ?? CampaignValidation.ValidateCampaignPeriod(startDate, endDate)
            ?? CampaignValidation.ValidateConsentDefaults(defaultConsentChannel, defaultConsentPurpose)
            // Format-level only: MOD-0290 / MOD-0162 have no runtime, so there is no master to resolve against.
            // FU08 format level only — existence / active / containment need a read and live in the guard.
            ?? CampaignValidation.ValidateCyclePeriodReference(cyclePeriodId);

        return error is null ? (null, 0) : (error, 400);
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// FU10 — applies the accepted targeting to the aggregate, in ONE place so create and update cannot drift.
    ///
    /// <para><b>Switching the mode never clears the other mode's data.</b> The targeted segments are replaced with
    /// what the caller sent (a full replace, like every other field on the command) and the manual target rows are
    /// separate documents nothing here touches. A campaign switched to manual keeps its segments dormant, and one
    /// switched back finds them exactly as they were.</para>
    ///
    /// <para><c>LinkedAt</c> is preserved for a segment that was already linked, so re-saving a campaign does not
    /// rewrite when its audience was chosen.</para>
    /// </summary>
    public static void ApplyTargeting(
        CampaignEntity campaign, CampaignSegmentValidator.Result targeting, DateTimeOffset now)
    {
        campaign.TargetingMode = targeting.TargetingMode!;

        var existing = campaign.TargetedSegments.ToDictionary(s => s.SegmentId, s => s.LinkedAt);
        campaign.TargetedSegments = (targeting.SegmentIds ?? Array.Empty<Guid>())
            .Select(id => new CampaignTargetedSegment
            {
                SegmentId = id,
                LinkedAt = existing.TryGetValue(id, out var linkedAt) ? linkedAt : now
            })
            .ToList();
    }

    /// <summary>Cross-record duplicate-mapping guard. A (SourceSystem, ExternalId) pair already owned by another
    /// non-archived campaign is a reported conflict — never a silent merge of two legacy campaign histories.</summary>
    public static async Task<string?> FindExternalMappingConflictAsync(
        ICampaignRepository repository,
        Guid tenantId,
        Guid? selfId,
        IReadOnlyList<CampaignExternalReferenceInput>? references,
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
                       $"owned by campaign {existing.Id}. Silent merge is not performed — resolve the duplicate " +
                       "mapping explicitly.";
            }
        }

        return null;
    }
}

public sealed class CreateCampaignHandler : IRequestHandler<CreateCampaignCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICampaignRepository _repository;
    private readonly CampaignCycleBindingGuard _cycleBinding;
    private readonly CampaignScopeWriteValidator _scope;
    private readonly CampaignSegmentValidator _targeting;
    private readonly ICampaignCodeGenerator _codeGenerator;

    public CreateCampaignHandler(
        ITenantContext tenant,
        IActorContext actor,
        ICampaignRepository repository,
        CampaignCycleBindingGuard cycleBinding,
        CampaignScopeWriteValidator scope,
        CampaignSegmentValidator targeting,
        ICampaignCodeGenerator codeGenerator)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _cycleBinding = cycleBinding;
        _scope = scope;
        _targeting = targeting;
        _codeGenerator = codeGenerator;
    }

    public async Task<Response<Guid>> Handle(CreateCampaignCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }


        var (error, statusCode) = CampaignWrite.Validate(
            request.CampaignName, request.CampaignType, request.CampaignStatus, request.ObjectiveType,
            request.StartDate, request.EndDate, request.DefaultConsentChannel, request.DefaultConsentPurpose,
            request.CyclePeriodId);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, statusCode);
        }

        // FU09 — the address first: the cycle guard needs an ACCEPTED scope to decide applicability against, and a
        // malformed or unprovable scope must never reach a period lookup.
        var scopeResult = await _scope.ValidateAsync(
            request.ScopeType, request.CountryScope, request.LegalEntityId, request.BusinessUnitId,
            current: null, cancellationToken);
        if (scopeResult.Failure is { } scopeFailure)
        {
            return Response<Guid>.Fail(scopeFailure.Error, scopeFailure.StatusCode);
        }

        var scope = scopeResult.Scope!;

        // FU10 — the targeting gate: mode + set shape + the segments being added. Before any write, like every other
        // gate on this path.
        var targeting = await _targeting.ValidateAsync(
            request.TargetingMode, request.TargetedSegmentIds, current: null, cancellationToken);
        if (targeting.Failure is { } targetingFailure)
        {
            return Response<Guid>.Fail(targetingFailure.Error, targetingFailure.StatusCode);
        }

        // FU08 — prove the binding BEFORE anything is written. On create there is no current binding, so a supplied
        // period is always a binding CHANGE and must therefore be active. FU09 adds the scope-applicability step.
        var binding = await _cycleBinding.EvaluateAsync(
            request.CyclePeriodId, currentCyclePeriodId: null, request.StartDate, request.EndDate,
            scope.ScopeType, scope.ScopeRef, cancellationToken);
        if (!binding.IsAllowed)
        {
            return Response<Guid>.Fail(binding.Error!, 400);
        }

        // FU10 — an empty code asks for a generated one. Generation happens HERE, at write time, so an abandoned
        // create screen never burns a sequence number.
        string campaignCode;
        if (string.IsNullOrWhiteSpace(request.CampaignCode))
        {
            try
            {
                campaignCode = await _codeGenerator.GenerateAsync(tenantId, cancellationToken);
            }
            catch (CampaignCodeGenerationException ex)
            {
                return Response<Guid>.Fail(ex.Message, 500);
            }
        }
        else
        {
            campaignCode = request.CampaignCode.Trim();
        }

        if (await _repository.GetActiveByCodeAsync(tenantId, campaignCode, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived campaign already uses CampaignCode '{campaignCode}' (campaignId={duplicate.Id}). " +
                "CampaignCode must be unique among active campaigns.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var campaign = new CampaignEntity
        {
            TenantId = tenantId,
            CampaignCode = campaignCode,
            CampaignName = request.CampaignName.Trim(),
            CampaignType = CampaignTypes.Normalize(request.CampaignType),
            CampaignStatus = CampaignStatuses.Normalize(request.CampaignStatus),
            ObjectiveType = string.IsNullOrWhiteSpace(request.ObjectiveType)
                ? null
                : CampaignObjectiveTypes.Normalize(request.ObjectiveType),
            DefaultConsentChannel = string.IsNullOrWhiteSpace(request.DefaultConsentChannel)
                ? null
                : ConsentChannel.Normalize(request.DefaultConsentChannel),
            DefaultConsentPurpose = string.IsNullOrWhiteSpace(request.DefaultConsentPurpose)
                ? null
                : ConsentPurpose.Normalize(request.DefaultConsentPurpose),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CyclePeriodId = request.CyclePeriodId,
            Description = CampaignWrite.Trim(request.Description),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        // FU09 - one place applies the address, so create and update cannot drift.
        CampaignScopeRules.Apply(campaign, scope);
        CampaignWrite.ApplyTargeting(campaign, targeting, now);

        await _repository.InsertAsync(campaign, cancellationToken);
        return Response<Guid>.Success(campaign.Id, 201);
    }
}

public sealed class UpdateCampaignHandler : IRequestHandler<UpdateCampaignCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICampaignRepository _repository;
    private readonly CampaignCycleBindingGuard _cycleBinding;
    private readonly CampaignScopeWriteValidator _scope;
    private readonly CampaignSegmentValidator _targeting;

    public UpdateCampaignHandler(
        ITenantContext tenant,
        IActorContext actor,
        ICampaignRepository repository,
        CampaignCycleBindingGuard cycleBinding,
        CampaignScopeWriteValidator scope,
        CampaignSegmentValidator targeting)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _cycleBinding = cycleBinding;
        _scope = scope;
        _targeting = targeting;
    }

    public async Task<Response<bool>> Handle(UpdateCampaignCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var campaign = await _repository.GetByIdAsync(tenantId, request.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return Response<bool>.Fail("Campaign not found.", 404);
        }

        if (campaign.IsArchived())
        {
            return Response<bool>.Fail(
                "An archived campaign cannot be updated. Archived campaigns are read-only history.", 409);
        }

        // Archiving is a dedicated endpoint (it stamps ArchivedAt/By); update never archives.
        if (string.Equals(request.CampaignStatus?.Trim(), CampaignStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail(
                "Use the archive endpoint to archive a campaign; update cannot set status=archived.", 400);
        }

        var (error, statusCode) = CampaignWrite.Validate(
            request.CampaignName, request.CampaignType, request.CampaignStatus, request.ObjectiveType,
            request.StartDate, request.EndDate, request.DefaultConsentChannel, request.DefaultConsentPurpose,
            request.CyclePeriodId);
        if (error is not null)
        {
            return Response<bool>.Fail(error, statusCode);
        }

        // FU09 — the campaign is passed in so the validator can tell whether the business-unit reference CHANGED. An
        // untouched pre-FU09 code is not re-validated: a campaign must not become uneditable because of data that
        // predates the governed set.
        var scopeResult = await _scope.ValidateAsync(
            request.ScopeType, request.CountryScope, request.LegalEntityId, request.BusinessUnitId,
            campaign, cancellationToken);
        if (scopeResult.Failure is { } scopeFailure)
        {
            return Response<bool>.Fail(scopeFailure.Error, scopeFailure.StatusCode);
        }

        var scope = scopeResult.Scope!;

        // FU10 — the stored campaign is passed in so the validator can tell which segments are newly ADDED: an
        // untouched segment that has since been archived is not re-validated, so the campaign stays editable.
        var targeting = await _targeting.ValidateAsync(
            request.TargetingMode, request.TargetedSegmentIds, campaign, cancellationToken);
        if (targeting.Failure is { } targetingFailure)
        {
            return Response<bool>.Fail(targetingFailure.Error, targetingFailure.StatusCode);
        }

        // FU08 — the stored binding is passed in so the guard can tell a CHANGED binding (must be active) from an
        // unchanged one (kept even after the period closed). FU09 evaluates applicability against the REQUESTED scope,
        // so moving a campaign to an address its bound period does not serve is refused rather than silently unbound.
        // Refusal happens before any field is mutated.
        var binding = await _cycleBinding.EvaluateAsync(
            request.CyclePeriodId, campaign.CyclePeriodId, request.StartDate, request.EndDate,
            scope.ScopeType, scope.ScopeRef, cancellationToken);
        if (!binding.IsAllowed)
        {
            return Response<bool>.Fail(binding.Error!, 400);
        }

        // CampaignCode is immutable — renaming goes through CampaignName.
        var now = DateTimeOffset.UtcNow;
        campaign.CampaignName = request.CampaignName.Trim();
        campaign.CampaignType = CampaignTypes.Normalize(request.CampaignType);
        campaign.CampaignStatus = CampaignStatuses.Normalize(request.CampaignStatus ?? campaign.CampaignStatus);
        campaign.ObjectiveType = string.IsNullOrWhiteSpace(request.ObjectiveType)
            ? null
            : CampaignObjectiveTypes.Normalize(request.ObjectiveType);
        CampaignScopeRules.Apply(campaign, scope);
        campaign.DefaultConsentChannel = string.IsNullOrWhiteSpace(request.DefaultConsentChannel)
            ? null
            : ConsentChannel.Normalize(request.DefaultConsentChannel);
        campaign.DefaultConsentPurpose = string.IsNullOrWhiteSpace(request.DefaultConsentPurpose)
            ? null
            : ConsentPurpose.Normalize(request.DefaultConsentPurpose);
        campaign.StartDate = request.StartDate;
        campaign.EndDate = request.EndDate;
        campaign.CyclePeriodId = request.CyclePeriodId;
        campaign.Description = CampaignWrite.Trim(request.Description);
        CampaignWrite.ApplyTargeting(campaign, targeting, now);
        campaign.UpdatedAt = now;
        campaign.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(campaign, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveCampaignHandler : IRequestHandler<ArchiveCampaignCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICampaignRepository _repository;

    public ArchiveCampaignHandler(ITenantContext tenant, IActorContext actor, ICampaignRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(ArchiveCampaignCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var campaign = await _repository.GetByIdAsync(tenantId, request.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return Response<bool>.Fail("Campaign not found.", 404);
        }

        if (campaign.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        // Existing targets are deliberately NOT cascaded: a silent cascade would rewrite targeting history. The
        // campaign status is visible to consumers, and no new target mutation is accepted from here on.
        var now = DateTimeOffset.UtcNow;
        campaign.CampaignStatus = CampaignStatuses.Archived;
        campaign.ArchivedAt = now;
        campaign.ArchivedBy = _actor.ActorName;
        campaign.UpdatedAt = now;
        campaign.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(campaign, cancellationToken);
        return Response<bool>.Success(true);
    }
}
