using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Campaign.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Features.Campaign.Handlers;

/// <summary>Shared FU04 target write-path rules. A target is never authorable without a reason, and an archived
/// campaign accepts no target mutation at all.</summary>
internal static class CampaignTargetWrite
{
    /// <summary>
    /// MOD-0165 FU11 - the fields the manual screen stopped asking for, filled in from what the server already knows.
    /// <para>This is the whole point of the redesign: an author was being asked to retype facts (the source is manual,
    /// the moment is now, the selector is the signed-in actor) that the system could state more accurately itself.
    /// A caller that DOES supply a value always wins - the defaults fill gaps, they never overwrite.</para>
    /// <para>FU04's invariant is untouched: every target still states why it exists. What changed is who writes the
    /// sentence, and the server's sentence is a fact rather than prose someone had to invent.</para>
    /// </summary>
    public static (string TargetSource, string SelectionReason, DateTimeOffset EffectiveFrom, bool ReasonGenerated)
        ResolveManualDefaults(
            string? targetSource,
            string? selectionReason,
            DateTimeOffset? effectiveFrom,
            string? existingSelectionReason,
            string? actorName,
            DateTimeOffset now)
    {
        var source = string.IsNullOrWhiteSpace(targetSource) ? CampaignTargetSources.Manual : targetSource.Trim();

        // Precedence: what the caller said > what the target already says > a generated statement of fact.
        // The middle step matters on update - an edit that never mentioned the reason must not erase it.
        var supplied = CampaignWrite.Trim(selectionReason) ?? CampaignWrite.Trim(existingSelectionReason);
        var generated = supplied is null;
        var reason = supplied ?? BuildSelectionReason(actorName, now);

        return (source, reason, effectiveFrom ?? now, generated);
    }

    /// <summary>The generated selection reason. It states only what is actually known - who selected the target and
    /// when - and says "an operator" rather than inventing a name when the actor is anonymous.</summary>
    public static string BuildSelectionReason(string? actorName, DateTimeOffset now)
    {
        var actor = string.IsNullOrWhiteSpace(actorName) ? "an operator" : actorName.Trim();
        return $"Manually selected by {actor} on {now:yyyy-MM-dd}.";
    }

    /// <summary>Rules shared by manual create and update. Returns (error, statusCode) or (null, 0).
    /// <para>Callers pass the values AFTER <see cref="ResolveManualDefaults"/> has run, so the FU04 checks below still
    /// guard every write - they simply never fire for a manual author who left the field blank.</para></summary>
    public static (string? Error, int StatusCode) Validate(
        string targetSource,
        string selectionReason,
        string? targetStatus,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        int? priority,
        string? priorityLevel,
        string? sourceReferenceType,
        Guid? sourceReferenceId,
        string? exclusionReason,
        IReadOnlyList<CampaignExternalReferenceInput>? externalReferences)
    {
        var error = CampaignValidation.ValidateTargetSource(targetSource)
            ?? CampaignValidation.ValidateSelectionReason(selectionReason)
            ?? CampaignValidation.ValidateAuthorableTargetStatus(targetStatus)
            ?? CampaignValidation.ValidateEffectiveRange(effectiveFrom, effectiveTo)
            ?? CampaignValidation.ValidatePriority(priority)
            ?? CampaignValidation.ValidatePriorityLevel(priorityLevel)
            ?? CampaignValidation.ValidateSourceReference(sourceReferenceType, sourceReferenceId)
            ?? CampaignValidation.ValidateExclusion(targetStatus, exclusionReason);

        if (error is not null)
        {
            return (error, 400);
        }

        var (referenceError, isConflict) = CampaignValidation.ValidateExternalReferences(externalReferences);
        return referenceError is null ? (null, 0) : (referenceError, isConflict ? 409 : 400);
    }

    /// <summary>
    /// Loads the campaign and refuses target mutation when it is archived. Returns the campaign, or an error response
    /// payload. Archived campaign ⇒ 409 with the <c>campaign_archived_no_target_mutation</c> reason in the message.
    /// </summary>
    public static async Task<(CampaignEntity? Campaign, string? Error, int StatusCode)> LoadMutableCampaignAsync(
        ICampaignRepository campaigns, Guid tenantId, Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(tenantId, campaignId, cancellationToken);
        if (campaign is null)
        {
            return (null, "Campaign not found.", 404);
        }

        if (campaign.IsArchived())
        {
            return (null,
                $"Campaign {campaignId} is archived; its targets can be read but never mutated " +
                $"({CampaignReasonCodes.CampaignArchivedNoTargetMutation}).",
                409);
        }

        // MOD-0165 FU10 - the targeting-mode gate. A segment-targeted campaign does not accept NEW manual targets.
        //
        // This is the ONE place it is enforced, and it is here rather than in each handler because all three write
        // paths (manual create, manual update, snapshot) already come through this loader - a rule written three
        // times is three rules. Archiving a target deliberately does NOT pass through here: closing history is not
        // adding data, so an existing row can always be retired whatever the mode.
        //
        // It refuses new rows only. Rows authored while the campaign was in manual mode stay exactly where they are
        // and become writable again the moment the mode is switched back; switching a mode is not a deletion.
        return campaign.IsSegmentTargeted()
            ? (null,
                "This campaign is targeted by segment, so a manual target cannot be added or changed " +
                $"({CampaignReasonCodes.CampaignTargetingModeForbidsManualTarget}). Its existing manual targets are " +
                "preserved and become editable again if the targeting mode is switched back to manual.",
                400)
            : (campaign, null, 0);
    }

    /// <summary>Reason codes always carry at least the lifecycle code, and duplicates are collapsed — a target must
    /// never be explained twice or not at all.</summary>
    public static List<string> NormalizeReasonCodes(IReadOnlyList<string>? supplied, params string[] defaults)
    {
        var codes = new List<string>();
        foreach (var code in defaults.Concat(supplied ?? Array.Empty<string>()))
        {
            var trimmed = code?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !codes.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                codes.Add(trimmed);
            }
        }

        return codes;
    }
}

public sealed class CreateCampaignTargetHandler : IRequestHandler<CreateCampaignTargetCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICampaignRepository _campaigns;
    private readonly ICampaignTargetRepository _targets;

    public CreateCampaignTargetHandler(
        ITenantContext tenant, IActorContext actor, ICampaignRepository campaigns, ICampaignTargetRepository targets)
    {
        _tenant = tenant;
        _actor = actor;
        _campaigns = campaigns;
        _targets = targets;
    }

    public async Task<Response<Guid>> Handle(CreateCampaignTargetCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (CampaignValidation.ValidateTargetType(request.TargetType) is { } targetTypeError)
        {
            return Response<Guid>.Fail(targetTypeError, 400);
        }

        if (CampaignValidation.ValidateTargetId(request.TargetId) is { } targetIdError)
        {
            return Response<Guid>.Fail(targetIdError, 400);
        }

        var now = DateTimeOffset.UtcNow;
        var defaults = CampaignTargetWrite.ResolveManualDefaults(
            request.TargetSource, request.SelectionReason, request.EffectiveFrom,
            existingSelectionReason: null, _actor.ActorName, now);

        var (error, statusCode) = CampaignTargetWrite.Validate(
            defaults.TargetSource, defaults.SelectionReason, request.TargetStatus, defaults.EffectiveFrom,
            request.EffectiveTo, request.Priority, request.PriorityLevel, request.SourceReferenceType,
            request.SourceReferenceId, request.ExclusionReason, request.ExternalReferences);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, statusCode);
        }

        var (campaign, campaignError, campaignStatusCode) = await CampaignTargetWrite.LoadMutableCampaignAsync(
            _campaigns, tenantId, request.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return Response<Guid>.Fail(campaignError!, campaignStatusCode);
        }

        var targetType = CampaignTargetTypes.Normalize(request.TargetType);

        // The MANUAL path is strict: a human adding the same target twice is a mistake, not an idempotent retry.
        // (The snapshot path reconciles instead — see CreateCampaignTargetSnapshotHandler.)
        var existing = await _targets.FindActiveByTargetAsync(
            tenantId, request.CampaignId, targetType, request.TargetId, cancellationToken);
        if (existing is not null)
        {
            return Response<Guid>.Fail(
                $"Campaign {request.CampaignId} already has a non-archived target for " +
                $"{targetType}/{request.TargetId} (campaignTargetId={existing.Id}) — " +
                $"{CampaignReasonCodes.CampaignTargetDuplicate}.", 409);
        }

        // A generated reason is DECLARED, not hidden: the row records that the server wrote its own justification, so
        // an auditor can tell a stated reason from a filled-in one without comparing wording.
        var lifecycleCodes = defaults.ReasonGenerated
            ? new[]
            {
                CampaignReasonCodes.CampaignTargetCreated,
                CampaignReasonCodes.ManualTargetSelected,
                CampaignReasonCodes.CampaignTargetSelectionReasonGenerated
            }
            : new[]
            {
                CampaignReasonCodes.CampaignTargetCreated,
                CampaignReasonCodes.ManualTargetSelected
            };

        var target = new CampaignTarget
        {
            TenantId = tenantId,
            CampaignId = request.CampaignId,
            TargetType = targetType,
            TargetId = request.TargetId,
            TargetDisplayName = CampaignWrite.Trim(request.TargetDisplayName),
            TargetStatus = CampaignTargetStatuses.Normalize(request.TargetStatus),
            TargetSource = CampaignTargetSources.Normalize(defaults.TargetSource),
            SourceReferenceType = CampaignWrite.Trim(request.SourceReferenceType)?.ToLowerInvariant(),
            SourceReferenceId = request.SourceReferenceId,
            SnapshotBatchId = null, // manual authoring is not part of a snapshot batch
            SelectionReason = defaults.SelectionReason,
            ReasonCodes = CampaignTargetWrite.NormalizeReasonCodes(request.ReasonCodes, lifecycleCodes),
            Priority = request.Priority,
            PriorityLevel = CampaignTargetPriorityLevels.Normalize(request.PriorityLevel),
            ConsentEvaluation = null, // consent provenance is only ever written from a live MOD-0164 evaluation
            EffectiveFrom = defaults.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            ExclusionReason = CampaignWrite.Trim(request.ExclusionReason),
            Notes = CampaignWrite.Trim(request.Notes),
            ExternalReferences = CampaignMapper.ToEntities(request.ExternalReferences, now),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _targets.InsertAsync(target, cancellationToken);
        return Response<Guid>.Success(target.Id, 201);
    }
}

public sealed class UpdateCampaignTargetHandler : IRequestHandler<UpdateCampaignTargetCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICampaignRepository _campaigns;
    private readonly ICampaignTargetRepository _targets;

    public UpdateCampaignTargetHandler(
        ITenantContext tenant, IActorContext actor, ICampaignRepository campaigns, ICampaignTargetRepository targets)
    {
        _tenant = tenant;
        _actor = actor;
        _campaigns = campaigns;
        _targets = targets;
    }

    public async Task<Response<bool>> Handle(UpdateCampaignTargetCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var target = await _targets.GetByIdAsync(tenantId, request.CampaignTargetId, cancellationToken);
        if (target is null || target.CampaignId != request.CampaignId)
        {
            return Response<bool>.Fail("Campaign target not found.", 404);
        }

        if (target.IsArchived())
        {
            return Response<bool>.Fail(
                "An archived campaign target cannot be updated. Archived targets are read-only history.", 409);
        }

        var (campaign, campaignError, campaignStatusCode) = await CampaignTargetWrite.LoadMutableCampaignAsync(
            _campaigns, tenantId, request.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return Response<bool>.Fail(campaignError!, campaignStatusCode);
        }

        if (string.Equals(request.TargetStatus?.Trim(), CampaignTargetStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail(
                "Use the archive endpoint to archive a target; update cannot set status=archived.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var defaults = CampaignTargetWrite.ResolveManualDefaults(
            request.TargetSource ?? target.TargetSource, request.SelectionReason, request.EffectiveFrom,
            target.SelectionReason, _actor.ActorName, target.EffectiveFrom);

        var (error, statusCode) = CampaignTargetWrite.Validate(
            defaults.TargetSource, defaults.SelectionReason, request.TargetStatus, defaults.EffectiveFrom,
            request.EffectiveTo, request.Priority, request.PriorityLevel, request.SourceReferenceType,
            request.SourceReferenceId, request.ExclusionReason, request.ExternalReferences);
        if (error is not null)
        {
            return Response<bool>.Fail(error, statusCode);
        }

        // CampaignId and TargetType/TargetId are IMMUTABLE — a different target is a different record.
        // ConsentEvaluation is NOT settable here: a caller may never hand-craft a consent verdict.
        target.TargetDisplayName = CampaignWrite.Trim(request.TargetDisplayName);
        target.TargetStatus = CampaignTargetStatuses.Normalize(request.TargetStatus ?? target.TargetStatus);
        target.TargetSource = CampaignTargetSources.Normalize(defaults.TargetSource);
        target.SourceReferenceType = CampaignWrite.Trim(request.SourceReferenceType)?.ToLowerInvariant();
        target.SourceReferenceId = request.SourceReferenceId;
        target.SelectionReason = defaults.SelectionReason;
        target.ReasonCodes = CampaignTargetWrite.NormalizeReasonCodes(
            request.ReasonCodes, CampaignReasonCodes.CampaignTargetUpdated);
        // The deprecated integer is PRESERVED unless the caller explicitly sends one. The FU11 screen never sends it, so
        // without this an edit would silently erase the only record that a pre-FU11 row was ever prioritised.
        target.Priority = request.Priority ?? target.Priority;
        target.PriorityLevel = CampaignTargetPriorityLevels.Normalize(request.PriorityLevel);
        target.EffectiveFrom = defaults.EffectiveFrom;
        target.EffectiveTo = request.EffectiveTo;
        target.ExclusionReason = CampaignWrite.Trim(request.ExclusionReason);
        target.Notes = CampaignWrite.Trim(request.Notes);
        target.ExternalReferences = CampaignMapper.ToEntities(request.ExternalReferences, now);
        target.UpdatedAt = now;
        target.UpdatedBy = _actor.ActorName;

        await _targets.UpdateAsync(target, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveCampaignTargetHandler : IRequestHandler<ArchiveCampaignTargetCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICampaignTargetRepository _targets;

    public ArchiveCampaignTargetHandler(ITenantContext tenant, IActorContext actor, ICampaignTargetRepository targets)
    {
        _tenant = tenant;
        _actor = actor;
        _targets = targets;
    }

    public async Task<Response<bool>> Handle(ArchiveCampaignTargetCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var target = await _targets.GetByIdAsync(tenantId, request.CampaignTargetId, cancellationToken);
        if (target is null || target.CampaignId != request.CampaignId)
        {
            return Response<bool>.Fail("Campaign target not found.", 404);
        }

        if (target.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        // Archiving a target is allowed even for an archived campaign: closing history is not mutating targeting.
        var now = DateTimeOffset.UtcNow;
        target.TargetStatus = CampaignTargetStatuses.Archived;
        target.ArchivedAt = now;
        target.ArchivedBy = _actor.ActorName;
        target.ReasonCodes = CampaignTargetWrite.NormalizeReasonCodes(
            target.ReasonCodes, CampaignReasonCodes.CampaignTargetArchived);
        target.UpdatedAt = now;
        target.UpdatedBy = _actor.ActorName;

        await _targets.UpdateAsync(target, cancellationToken);
        return Response<bool>.Success(true);
    }
}
