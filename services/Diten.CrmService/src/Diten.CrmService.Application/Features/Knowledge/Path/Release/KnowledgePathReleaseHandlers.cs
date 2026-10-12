using System.Globalization;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Application.Features.Knowledge.Content;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Release;

/// <summary>Shared loading of the release handlers: tenant, path, the revision (must belong to the path).</summary>
internal static class KnowledgePathReleaseLoad
{
    public static async Task<(Guid TenantId, KnowledgePath? Path, KnowledgePathRevision? Revision, Response<T>? Error)> LoadAsync<T>(
        ITenantContext tenant, IKnowledgePathRepository paths, IKnowledgePathRevisionRepository revisions,
        Guid pathId, Guid revisionId, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return (Guid.Empty, null, null, Response<T>.Fail("Tenant context is required.", 400));
        }

        var path = await paths.GetByIdAsync(tenantId, pathId, ct);
        var revision = path is null ? null : await revisions.GetByIdAsync(tenantId, revisionId, ct);
        return revision is null || revision.PathId != path!.Id
            ? (tenantId, null, null, Response<T>.Fail("Revision not found.", 404))
            : (tenantId, path, revision, null);
    }

    public static Response<T> Fail<T>(string code, string message, int status) => KnowledgePathReviewRules.Fail<T>(code, message, status);

    public static bool IsApproved(KnowledgePathRevision r)
        => r.Status == KnowledgePathRevisionStatuses.Approved
           || r.ReleaseState is not null; // a released / withdrawn revision was approved

    public static KnowledgePathRenderedArtifact? Pdf(KnowledgePathRevision r)
        => r.RenderedArtifacts.FirstOrDefault(a => a.Kind == KnowledgePathArtifactKinds.Pdf);

    public static KnowledgePathArtifactDto ToDto(KnowledgePathRenderedArtifact a)
        => new(a.Kind, a.ContentId, a.Checksum, a.ByteSize, a.MediaType, a.FileName, a.RenderedAt, a.RenderedBy);

    public static KnowledgePathStageRefDto ToRef(KnowledgePathJourneyUse use)
        => new(use.Journey.Id, use.Journey.JourneyCode, use.Journey.JourneyName, use.Stage.StageCode, use.Stage.StageName);
}

/// <summary>
/// WP-KP-3 (§3.4) — renders the APPROVED revision into its archive PDF and stores it in MOD-0262-FU01
/// (ContentMessagingArtifacts, owner = the revision, caller's token). Idempotent: an existing pdf is returned with its
/// content id. The MLR round is read from MOD-0023 (step names + comments); when it cannot be read nothing is rendered
/// (503) — an archive copy without its approval trail is never produced. The store is fail-closed (no pointer bound on
/// a failed upload).
/// </summary>
public sealed class RenderKnowledgePathRevisionHandler
    : IRequestHandler<RenderKnowledgePathRevisionCommand, Response<KnowledgePathArtifactDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IClaimCountryVersionRepository _claimVersions;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IConceptTypeRepository _types;
    private readonly IAudienceProfileRepository _profiles;
    private readonly IWorkflowDecisionClient _workflow;
    private readonly IKnowledgePathRevisionRenderer _renderer;
    private readonly IContentArtifactStore _store;

    public RenderKnowledgePathRevisionHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, IKnowledgeContentRepository contents,
        IClaimCountryVersionRepository claimVersions, IConceptChainTemplateRepository templates, IConceptTypeRepository types,
        IAudienceProfileRepository profiles, IWorkflowDecisionClient workflow, IKnowledgePathRevisionRenderer renderer,
        IContentArtifactStore store)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _revisions = revisions;
        _contents = contents;
        _claimVersions = claimVersions;
        _templates = templates;
        _types = types;
        _profiles = profiles;
        _workflow = workflow;
        _renderer = renderer;
        _store = store;
    }

    public async Task<Response<KnowledgePathArtifactDto>> Handle(RenderKnowledgePathRevisionCommand request, CancellationToken ct)
    {
        var (tenantId, path, revision, error) = await KnowledgePathReleaseLoad.LoadAsync<KnowledgePathArtifactDto>(
            _tenant, _paths, _revisions, request.PathId, request.RevisionId, ct);
        if (error is not null)
        {
            return error;
        }

        if (!KnowledgePathReleaseLoad.IsApproved(revision!))
        {
            return KnowledgePathReleaseLoad.Fail<KnowledgePathArtifactDto>(KnowledgePathReleaseErrors.RevisionNotApproved,
                "Only an approved revision can be rendered.", 409);
        }

        if (KnowledgePathReleaseLoad.Pdf(revision!) is { } existing)
        {
            return Response<KnowledgePathArtifactDto>.Success(KnowledgePathReleaseLoad.ToDto(existing)); // idempotent
        }

        var history = await _workflow.GetInstanceHistoryAsync(revision!.ReviewRound.WorkflowInstanceId, ct);
        if (history is null)
        {
            return KnowledgePathReleaseLoad.Fail<KnowledgePathArtifactDto>(ClaimErrorCodes.WorkflowUnavailable,
                "The MLR review history cannot be read; the archive copy is not produced without it.", 503);
        }

        var now = DateTimeOffset.UtcNow;
        var model = await BuildModelAsync(tenantId, path!, revision, history, now, ct);
        var rendered = _renderer.Render(model);

        ContentArtifactStoreResult stored;
        try
        {
            stored = await _store.StoreAsync(new ContentArtifactStoreRequest(revision.Id, revision.PathId, rendered.FileName,
                rendered.MediaType, rendered.Bytes), ct);
        }
        catch (ContentArtifactStoreException ex)
        {
            return KnowledgePathReleaseLoad.Fail<KnowledgePathArtifactDto>(KnowledgePathReleaseErrors.ArtifactStoreUnavailable,
                ex.Message, ex.StatusCode is >= 400 and < 600 ? ex.StatusCode : 503);
        }

        var artifact = new KnowledgePathRenderedArtifact
        {
            Kind = KnowledgePathArtifactKinds.Pdf, ContentId = stored.ContentId, Checksum = stored.Checksum,
            ByteSize = stored.ByteSize, MediaType = stored.MediaType, FileName = rendered.FileName, RenderedAt = now,
            RenderedBy = _actor.ActorName
        };
        revision.RenderedArtifacts.Add(artifact);
        revision.UpdatedAt = now;
        revision.UpdatedBy = _actor.ActorName;
        return await _revisions.ReplaceAsync(revision, revision.Version, ct)
            ? Response<KnowledgePathArtifactDto>.Success(KnowledgePathReleaseLoad.ToDto(artifact), 201)
            : Response<KnowledgePathArtifactDto>.Fail("The revision was modified by another writer; reload and retry.", 409);
    }

    private async Task<KnowledgePathRenderModel> BuildModelAsync(Guid tenantId, KnowledgePath path, KnowledgePathRevision revision,
        IReadOnlyList<WorkflowHistoryEntry> history, DateTimeOffset now, CancellationToken ct)
    {
        var snapshot = revision.Snapshot;
        var template = await _templates.GetByIdAsync(tenantId, snapshot.ConceptChainTemplateId, ct);
        var branchNames = template?.Branches.ToDictionary(b => b.BranchCode.ToUpperInvariant(), b => b.BranchName ?? b.BranchCode)
                          ?? new Dictionary<string, string>();

        var slots = new List<KnowledgePathRenderSlot>();
        foreach (var row in snapshot.Conformance)
        {
            var type = await _types.GetByIdAsync(tenantId, row.ChainStepId, ct);
            var contents = new List<KnowledgePathRenderContent>();
            foreach (var step in snapshot.Steps.Where(s => Same(s.Arrangement, row.BranchCode, row.ChainStepId)).OrderBy(s => s.StepOrder))
            {
                var content = await _contents.GetByIdAsync(tenantId, step.ContentId, ct);
                contents.Add(new KnowledgePathRenderContent(snapshot.Steps.OrderBy(s => s.StepOrder).ToList().IndexOf(step) + 1,
                    content?.ContentTitle ?? step.StepTitle, step.ContentCode, step.ContentVersion, step.IsRequired));
            }

            var claims = new List<KnowledgePathRenderClaim>();
            foreach (var claim in snapshot.Claims.Where(c => Same(c.Arrangement, row.BranchCode, row.ChainStepId))
                         .OrderBy(c => c.Arrangement.Position))
            {
                var version = claim.CountryVersionId is { } vid ? await _claimVersions.GetByIdAsync(tenantId, vid, ct) : null;
                claims.Add(new KnowledgePathRenderClaim(claim.ClaimCode,
                    InLanguage(version?.Texts, snapshot.LanguageCode), InLanguage(version?.Qualifiers, snapshot.LanguageCode),
                    claim.CountryVersion));
            }

            slots.Add(new KnowledgePathRenderSlot(branchNames.GetValueOrDefault(row.BranchCode.ToUpperInvariant(), row.BranchCode),
                type?.ConceptTypeName ?? row.ChainStepId.ToString("D"), row.Count, row.Min, row.Max, row.Status, contents, claims));
        }

        var audiences = new List<string>();
        foreach (var id in snapshot.AudienceProfileIds)
        {
            if (await _profiles.GetByIdAsync(tenantId, id, ct) is { } profile)
            {
                audiences.Add(profile.ProfileName);
            }
        }

        return new KnowledgePathRenderModel(
            ApprovalCode(revision),
            revision.PathCode, snapshot.PathName, revision.PathVersion, revision.RevisionNumber,
            template?.ChainName, snapshot.ChainVersion,
            CountryName(snapshot.CountryCode), LanguageName(snapshot.LanguageCode),
            snapshot.ProductName ?? snapshot.ProductCode, audiences, slots,
            history.OrderBy(h => h.SequenceNo).Select(h => new KnowledgePathRenderReviewEntry(
                h.Action, h.StepName, h.ActorDisplay ?? h.ActorId, h.OccurredAt, h.Comment)).ToList(),
            revision.CreatedBy, now, _actor.ActorName);
    }

    /// <summary>The printed approval code: <c>{PathCode}-v{PathVersion}-R{n}</c>.</summary>
    public static string ApprovalCode(KnowledgePathRevision r) => $"{r.PathCode}-v{r.PathVersion}-R{r.RevisionNumber}";

    private static bool Same(KnowledgePathArrangement? a, string branchCode, Guid chainStepId)
        => a is not null && a.ChainStepId == chainStepId
           && string.Equals(a.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase);

    private static string? InLanguage(IEnumerable<ClaimLocalizedText>? texts, string? language)
        => texts?.FirstOrDefault(t => ChainContextValidation.SameLanguage(t.LanguageCode, language))?.Text;

    private static string? CountryName(string? code)
    {
        try
        {
            return string.IsNullOrWhiteSpace(code) ? null : new RegionInfo(code).NativeName;
        }
        catch (ArgumentException)
        {
            return code;
        }
    }

    private static string? LanguageName(string? code)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var culture = new CultureInfo(code);
            var name = culture.NativeName;
            return string.IsNullOrEmpty(name) ? code : culture.TextInfo.ToUpper(name[0]) + name[1..];
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }
}

/// <summary>WP-KP-3 — opens a revision's rendered artifact. The content id comes from the revision (never the client);
/// an unknown / other-tenant revision, an unrendered kind, or an object FU01 does not find is 404 (non-leakage).</summary>
public sealed class GetKnowledgePathRevisionArtifactHandler
    : IRequestHandler<GetKnowledgePathRevisionArtifactQuery, Response<ContentArtifactReadResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IContentArtifactStore _store;

    public GetKnowledgePathRevisionArtifactHandler(ITenantContext tenant, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, IContentArtifactStore store)
    {
        _tenant = tenant;
        _paths = paths;
        _revisions = revisions;
        _store = store;
    }

    public async Task<Response<ContentArtifactReadResult>> Handle(GetKnowledgePathRevisionArtifactQuery request, CancellationToken ct)
    {
        var (_, _, revision, error) = await KnowledgePathReleaseLoad.LoadAsync<ContentArtifactReadResult>(
            _tenant, _paths, _revisions, request.PathId, request.RevisionId, ct);
        var kind = string.IsNullOrWhiteSpace(request.Kind) ? KnowledgePathArtifactKinds.Pdf : request.Kind.Trim().ToLowerInvariant();
        var artifact = error is null ? revision!.RenderedArtifacts.FirstOrDefault(a => a.Kind == kind) : null;
        if (artifact is null)
        {
            return Response<ContentArtifactReadResult>.Fail("Rendered artifact not found.", 404);
        }

        var stream = await _store.OpenReadAsync(artifact.ContentId, ct);
        return stream is null
            ? Response<ContentArtifactReadResult>.Fail("Rendered artifact not found.", 404)
            : Response<ContentArtifactReadResult>.Success(stream);
    }
}

/// <summary>
/// WP-KP-3 (§3.5) — releases an approved, rendered revision. Fail-closed gate (409 + code, nothing written on a refusal):
/// <c>revision_not_approved</c>, <c>artifact_missing</c>, <c>revision_superseded</c> (not the path's latest approved
/// revision, or the path is no longer <c>approved</c>), person SoD (403 <c>sod_submitter_cannot_release</c>), then the
/// SB-2 rules on the FROZEN snapshot: <c>component_not_published</c>, <c>component_language_mismatch</c>,
/// <c>claim_no_country_version</c> / <c>claim_not_approved</c> / <c>claim_language_mismatch</c> (WP-CL-BE-6 gate),
/// <c>chain_conformance_failed</c>.
/// <para>Effect: the previously published version of the same PathCode → <c>inactive</c> (a warning
/// <c>previous_path_in_use</c> + the stage names when a published journey stage is pinned to it), the path →
/// <c>published</c> + frozen, the revision → Released. No multi-document transaction: a later failed write restores the
/// earlier ones. Idempotent: a released revision answers its current state.</para>
/// </summary>
public sealed class ReleaseKnowledgePathRevisionHandler
    : IRequestHandler<ReleaseKnowledgePathRevisionCommand, Response<KnowledgePathReleaseDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _claimVersions;
    private readonly IContentEngagementJourneyRepository _journeys;

    public ReleaseKnowledgePathRevisionHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, IKnowledgeContentRepository contents, IClaimRepository claims,
        IClaimCountryVersionRepository claimVersions, IContentEngagementJourneyRepository journeys)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _revisions = revisions;
        _contents = contents;
        _claims = claims;
        _claimVersions = claimVersions;
        _journeys = journeys;
    }

    public async Task<Response<KnowledgePathReleaseDto>> Handle(ReleaseKnowledgePathRevisionCommand request, CancellationToken ct)
    {
        var (tenantId, path, revision, error) = await KnowledgePathReleaseLoad.LoadAsync<KnowledgePathReleaseDto>(
            _tenant, _paths, _revisions, request.PathId, request.RevisionId, ct);
        if (error is not null)
        {
            return error;
        }

        if (revision!.ReleaseState is { State: KnowledgePathReleaseStates.Released } released)
        {
            return Response<KnowledgePathReleaseDto>.Success(Dto(revision, path!, released, [], [])); // idempotent
        }

        if (revision.Status != KnowledgePathRevisionStatuses.Approved || revision.ReleaseState is not null)
        {
            return Fail(KnowledgePathReleaseErrors.RevisionNotApproved, "Only an approved revision can be released.");
        }

        if (KnowledgePathReleaseLoad.Pdf(revision) is null)
        {
            return Fail(KnowledgePathReleaseErrors.ArtifactMissing, "Render the revision's PDF before releasing it.");
        }

        var latestApproved = (await _revisions.ListByPathAsync(tenantId, path!.Id, ct))
            .Where(r => r.Status == KnowledgePathRevisionStatuses.Approved).OrderBy(r => r.RevisionNumber).LastOrDefault();
        if (latestApproved?.Id != revision.Id
            || !string.Equals(path.PathStatus, KnowledgePathStatuses.Approved, StringComparison.Ordinal))
        {
            return Fail(KnowledgePathReleaseErrors.RevisionSuperseded,
                "Only the path's latest approved revision can be released, while the path is approved.");
        }

        if (!KnowledgePathReleaseRules.CanRelease(revision, _actor.ActorName))
        {
            return KnowledgePathReleaseLoad.Fail<KnowledgePathReleaseDto>(KnowledgePathReleaseErrors.SodSubmitterCannotRelease,
                "The submitter of a revision cannot release it.", 403);
        }

        if (await GateAsync(tenantId, revision.Snapshot, ct) is { } gate)
        {
            return gate;
        }

        // ---------------- writes (restored in reverse on a later failure) ----------------
        var now = DateTimeOffset.UtcNow;
        var actor = _actor.ActorName;
        var journeys = await _journeys.ListAsync(tenantId, ct);
        var warnings = new List<string>();
        var previousInUse = new List<KnowledgePathStageRefDto>();
        var deactivated = new List<(KnowledgePath Path, string Status)>();
        foreach (var previous in (await _paths.ListByCodeAsync(tenantId, path.PathCode, ct))
                     .Where(p => p.Id != path.Id && p.IsPublished() && !p.IsArchived()))
        {
            // Only a PINNED stage keeps showing the superseded version (a latest-published stage follows the new one).
            var uses = KnowledgePathReleaseRules.PublishedJourneyUses(journeys, previous, includeLatestPublished: false);
            if (uses.Count > 0)
            {
                warnings.Add(KnowledgePathReleaseErrors.PreviousPathInUse);
                previousInUse.AddRange(uses.Select(KnowledgePathReleaseLoad.ToRef));
            }

            var before = previous.PathStatus;
            previous.PathStatus = KnowledgePathStatuses.Inactive;
            previous.UpdatedAt = now;
            previous.UpdatedBy = actor;
            if (!await _paths.ReplaceAsync(previous, previous.Version, ct))
            {
                await RestoreAsync(deactivated, null, ct);
                return Conflict();
            }

            deactivated.Add((previous, before));
        }

        path.PathStatus = KnowledgePathStatuses.Published;
        path.StepSetFrozenAt = now;
        path.PublishedAt = now;
        path.PublishedBy = actor;
        path.UpdatedAt = now;
        path.UpdatedBy = actor;
        if (!await _paths.ReplaceAsync(path, path.Version, ct))
        {
            await RestoreAsync(deactivated, null, ct);
            return Conflict();
        }

        revision.ReleaseState = new KnowledgePathReleaseState { State = KnowledgePathReleaseStates.Released, At = now, By = actor };
        revision.UpdatedAt = now;
        revision.UpdatedBy = actor;
        if (!await _revisions.ReplaceAsync(revision, revision.Version, ct))
        {
            await RestoreAsync(deactivated, path, ct);
            return Conflict();
        }

        return Response<KnowledgePathReleaseDto>.Success(Dto(revision, path, revision.ReleaseState, warnings.Distinct().ToList(),
            previousInUse));
    }

    /// <summary>The SB-2 release rules on the frozen snapshot (shared: <see cref="KnowledgePathReleaseRules"/> + the
    /// WP-CL-BE-6 claim gate).</summary>
    private async Task<Response<KnowledgePathReleaseDto>?> GateAsync(Guid tenantId, KnowledgePathRevisionSnapshot snapshot, CancellationToken ct)
    {
        foreach (var step in snapshot.Steps)
        {
            var content = await _contents.GetByIdAsync(tenantId, step.ContentId, ct);
            if (!KnowledgePathReleaseRules.IsReleasableContent(content))
            {
                return Fail(KnowledgePathReviewErrors.ComponentNotPublished, $"Content '{step.ContentCode}' is no longer published.");
            }
        }

        if (KnowledgePathReleaseRules.DistinctLanguages(snapshot.Steps.Select(s => s.ContentLanguage).Append(snapshot.LanguageCode)).Count != 1)
        {
            return Fail(ChainContextErrors.ComponentLanguageMismatch, "The revision's contents are not all in the path language.");
        }

        if (snapshot.Claims.FirstOrDefault(c => c.CountryVersionId is null) is { } missing)
        {
            return Fail(KnowledgePathReviewErrors.ClaimNoCountryVersion,
                $"Claim '{missing.ClaimCode}' has no version in {snapshot.CountryCode}.");
        }

        var refs = snapshot.Claims.Select(c => new KnowledgeContentClaimRef
        {
            ClaimCode = c.ClaimCode, ClaimId = c.ClaimId, CountryVersionId = c.CountryVersionId, CountryCode = snapshot.CountryCode
        }).ToList();
        if (await KnowledgeContentClaimLinks.CheckPublishAsync(refs, snapshot.LanguageCode ?? string.Empty, tenantId, _claims,
                _claimVersions, ct) is { } claimGate)
        {
            return KnowledgePathReleaseLoad.Fail<KnowledgePathReleaseDto>(claimGate.Code, claimGate.Message, claimGate.StatusCode);
        }

        return snapshot.Conformance.Any(c => c.Status != KnowledgePathConformanceStatuses.Ok)
            ? Fail(KnowledgePathReviewErrors.ChainConformanceFailed, "The revision does not fill its chain.")
            : null;
    }

    private async Task RestoreAsync(List<(KnowledgePath Path, string Status)> deactivated, KnowledgePath? published, CancellationToken ct)
    {
        if (published is not null)
        {
            await TryAsync(async () =>
            {
                if (await _paths.GetByIdAsync(published.TenantId, published.Id, ct) is { } current)
                {
                    current.PathStatus = KnowledgePathStatuses.Approved;
                    current.StepSetFrozenAt = null;
                    current.PublishedAt = null;
                    current.PublishedBy = null;
                    await _paths.ReplaceAsync(current, current.Version, ct);
                }
            });
        }

        foreach (var (path, status) in deactivated)
        {
            await TryAsync(async () =>
            {
                if (await _paths.GetByIdAsync(path.TenantId, path.Id, ct) is { } current)
                {
                    current.PathStatus = status;
                    await _paths.ReplaceAsync(current, current.Version, ct);
                }
            });
        }
    }

    private static async Task TryAsync(Func<Task> step)
    {
        try
        {
            await step();
        }
        catch
        {
            // Compensation is best effort; the original failure is what the caller sees.
        }
    }

    private static Response<KnowledgePathReleaseDto> Fail(string code, string message)
        => KnowledgePathReleaseLoad.Fail<KnowledgePathReleaseDto>(code, message, 409);

    private static Response<KnowledgePathReleaseDto> Conflict()
        => Response<KnowledgePathReleaseDto>.Fail("The path was modified by another writer; nothing was released.", 409);

    internal static KnowledgePathReleaseDto Dto(KnowledgePathRevision revision, KnowledgePath path, KnowledgePathReleaseState state,
        IReadOnlyList<string> warnings, IReadOnlyList<KnowledgePathStageRefDto> previousInUse)
        => new(revision.Id, path.Id, state.State, state.At, state.By, state.Reason, path.PathStatus, warnings, previousInUse);
}

/// <summary>
/// WP-KP-3 (§3.6) — withdraws a released revision. The reason is mandatory (400 <c>reason_required</c>). When a
/// published journey stage uses the path (pinned, or following its code) nothing changes: 409 <c>path_in_use</c> with the
/// stage names (after the code and the message). Otherwise the path → <c>inactive</c> and the revision → Withdrawn.
/// </summary>
public sealed class WithdrawKnowledgePathReleaseHandler
    : IRequestHandler<WithdrawKnowledgePathReleaseCommand, Response<KnowledgePathReleaseDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IContentEngagementJourneyRepository _journeys;

    public WithdrawKnowledgePathReleaseHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, IContentEngagementJourneyRepository journeys)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _revisions = revisions;
        _journeys = journeys;
    }

    public async Task<Response<KnowledgePathReleaseDto>> Handle(WithdrawKnowledgePathReleaseCommand request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return KnowledgePathReleaseLoad.Fail<KnowledgePathReleaseDto>(KnowledgePathReleaseErrors.ReasonRequired,
                "A withdrawal needs a reason.", 400);
        }

        var (tenantId, path, revision, error) = await KnowledgePathReleaseLoad.LoadAsync<KnowledgePathReleaseDto>(
            _tenant, _paths, _revisions, request.PathId, request.RevisionId, ct);
        if (error is not null)
        {
            return error;
        }

        if (revision!.ReleaseState is not { State: KnowledgePathReleaseStates.Released })
        {
            return KnowledgePathReleaseLoad.Fail<KnowledgePathReleaseDto>(KnowledgePathReleaseErrors.RevisionNotReleased,
                "Only a released revision can be withdrawn.", 409);
        }

        var uses = KnowledgePathReleaseRules.PublishedJourneyUses(await _journeys.ListAsync(tenantId, ct), path!,
            includeLatestPublished: true);
        if (uses.Count > 0)
        {
            return Response<KnowledgePathReleaseDto>.Fail(
                new[] { KnowledgePathReleaseErrors.PathInUse, "A published journey stage uses this path; nothing was withdrawn." }
                    .Concat(uses.Select(u => u.Label)).ToList(), 409);
        }

        var now = DateTimeOffset.UtcNow;
        var before = path!.PathStatus;
        path.PathStatus = KnowledgePathStatuses.Inactive;
        path.UpdatedAt = now;
        path.UpdatedBy = _actor.ActorName;
        if (!await _paths.ReplaceAsync(path, path.Version, ct))
        {
            return Response<KnowledgePathReleaseDto>.Fail("The path was modified by another writer; nothing was withdrawn.", 409);
        }

        var previousState = revision.ReleaseState;
        revision.ReleaseState = new KnowledgePathReleaseState
        {
            State = KnowledgePathReleaseStates.Withdrawn, At = now, By = _actor.ActorName, Reason = reason
        };
        revision.UpdatedAt = now;
        revision.UpdatedBy = _actor.ActorName;
        if (!await _revisions.ReplaceAsync(revision, revision.Version, ct))
        {
            if (await _paths.GetByIdAsync(tenantId, path.Id, ct) is { } current)
            {
                current.PathStatus = before;
                await _paths.ReplaceAsync(current, current.Version, ct);
            }

            revision.ReleaseState = previousState;
            return Response<KnowledgePathReleaseDto>.Fail("The revision was modified by another writer; nothing was withdrawn.", 409);
        }

        return Response<KnowledgePathReleaseDto>.Success(ReleaseKnowledgePathRevisionHandler.Dto(revision, path, revision.ReleaseState, [], []));
    }
}

/// <summary>WP-KP-3 — where a path is used: journey stages (any journey status; pinned to this version or following its
/// code) and strategy templates bound to this concrete row (<c>knowledge-path</c>). No page-view data (later phase).</summary>
public sealed class GetKnowledgePathUsageHandler : IRequestHandler<GetKnowledgePathUsageQuery, Response<KnowledgePathUsageDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgePathRepository _paths;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly IStrategyTemplateRepository _strategies;

    public GetKnowledgePathUsageHandler(ITenantContext tenant, IKnowledgePathRepository paths,
        IContentEngagementJourneyRepository journeys, IStrategyTemplateRepository strategies)
    {
        _tenant = tenant;
        _paths = paths;
        _journeys = journeys;
        _strategies = strategies;
    }

    public async Task<Response<KnowledgePathUsageDto>> Handle(GetKnowledgePathUsageQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<KnowledgePathUsageDto>.Fail("Tenant context is required.", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, ct);
        if (path is null)
        {
            return Response<KnowledgePathUsageDto>.Fail("Knowledge path not found.", 404);
        }

        var journeys = (await _journeys.ListAsync(tenantId, ct))
            .Where(j => !j.IsArchived())
            .SelectMany(j => j.ActiveStages().Where(s => KnowledgePathReleaseRules.UsesPath(s, path, includeLatestPublished: true))
                .Select(s => new KnowledgePathJourneyUsageDto(j.Id, j.JourneyCode, j.JourneyName, j.JourneyStatus, s.StageCode,
                    s.StageName, s.PathVersionPinPolicy,
                    s.RecommendedKnowledgePathId == path.Id
                    && !string.Equals(s.PathVersionPinPolicy, ContentEngagementJourneyPathPin.LatestPublished, StringComparison.OrdinalIgnoreCase)
                        ? path.PathVersion
                        : null)))
            .OrderBy(u => u.Code, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.StageCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var strategies = (await _strategies.ListAsync(tenantId, ct))
            .Where(t => t.ContentBindings.Any(b => b.IsKnowledgePath() && b.ContentRefId == path.Id))
            .Select(t => new KnowledgePathStrategyUsageDto(t.Id, t.TemplateCode, t.TemplateName, t.TemplateStatus))
            .OrderBy(t => t.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Response<KnowledgePathUsageDto>.Success(new KnowledgePathUsageDto(journeys, strategies));
    }
}
