using System.Globalization;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge;
using Diten.CrmService.Application.Features.Knowledge.Content;
using Diten.CrmService.Application.Features.Knowledge.Content.Commands;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

/// <summary>WP-SB-2 — what a content set release produces and how a withdrawal retires it. The release handler owns the
/// release state; this port owns the knowledge side.</summary>
public interface IContentSetReleaseProducer
{
    /// <summary>Checks the release preconditions and, when they hold, produces the assembled-presentation content and the
    /// chain-ordered path (superseding the previous release's). Nothing is written when a precondition fails; a failure
    /// after the first write is compensated before returning.</summary>
    Task<ContentSetReleaseProduction> ProduceAsync(
        Guid tenantId, ContentSetRevision revision, ContentSetRenderedArtifact artifact, CancellationToken cancellationToken);

    /// <summary>Undoes a successful <see cref="ProduceAsync"/> (the revision save failed afterwards).</summary>
    Task CompensateAsync(Guid tenantId, ContentSetReleaseProduction production, CancellationToken cancellationToken);

    /// <summary>Withdrawal: the produced content → inactive; the produced path → inactive unless a published journey
    /// stage uses it (then untouched + <see cref="ContentSetReleaseWarnings.PathInUse"/>).</summary>
    Task<ContentSetReleaseProduction> RetireAsync(
        Guid tenantId, ContentSetRevision revision, CancellationToken cancellationToken);
}

/// <summary>WP-SB-2 — the outcome of a produce / retire call. <see cref="Errors"/> non-null = refused (the
/// <c>[code, message]</c> pair and <see cref="StatusCode"/> are returned to the caller unchanged).</summary>
public sealed class ContentSetReleaseProduction
{
    public IReadOnlyList<string>? Errors { get; private set; }
    public int StatusCode { get; private set; } = 200;
    public bool Succeeded => Errors is null;

    public Guid? ContentId { get; internal set; }
    public string? ContentCode { get; internal set; }
    public Guid? PathId { get; internal set; }
    public string? PathCode { get; internal set; }
    public List<string> Warnings { get; } = new();

    // Compensation bookkeeping (what this call changed on EARLIER releases' outputs).
    internal Guid? DeactivatedPreviousContentId { get; set; }
    internal Guid? DeactivatedPreviousPathId { get; set; }

    internal ContentSetReleaseProduction Fail(IReadOnlyList<string> errors, int statusCode)
    {
        Errors = errors;
        StatusCode = statusCode;
        return this;
    }

    internal ContentSetReleaseProduction Fail(string code, string message, int statusCode)
        => Fail(new[] { code, message }, statusCode);
}

/// <summary>
/// WP-SB-2 — the release → knowledge bridge (SCMM-studio-knowledge-bridge-decision §2 A + D). Every write goes through
/// the existing Knowledge commands (<see cref="ISender"/>), so the content / path validation, the WP-CL-BE-6 claim gate
/// and the path versioning / publish rules apply unchanged — no repository is written here. Standalone Mongo has no
/// multi-document transaction across these commands, so a failure after the first write is compensated (archive what
/// was created, re-publish what was superseded).
/// <para><b>Versioning</b> (user decision 2026-09-29, agent choice reported): a later revision of the same set opens a new
/// path VERSION through <see cref="CreateKnowledgePathVersionCommand"/> (same PathCode, <c>SupersedesPathId</c>) and the
/// previous version is set <c>inactive</c> before the new one is published — the V-P10 overlap rule only counts
/// published siblings, and inactive (not an EffectiveTo cut) is the same "superseded" state the content and the
/// withdrawal use. Content has no version command, so it gets a new record under a suffixed code and the previous
/// content is set <c>inactive</c>.</para>
/// </summary>
public sealed class ContentSetReleaseProducer : IContentSetReleaseProducer
{
    /// <summary>The Subject ExternalReference SourceSystem of an MDM Global Product link (taxonomy.js subject picker).</summary>
    internal const string GlobalProductSourceSystem = "global-product";

    private readonly ISender _sender;
    private readonly IContentSetRepository _sets;
    private readonly IContentSetRevisionRepository _revisions;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly ISubjectRepository _subjects;
    private readonly IContentScopeRepository _scopes;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IKnowledgePathRepository _paths;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _claimVersions;
    private readonly IReferenceDataCatalogReader? _catalog;

    public ContentSetReleaseProducer(
        ISender sender,
        IContentSetRepository sets,
        IContentSetRevisionRepository revisions,
        IConceptChainTemplateRepository templates,
        ISubjectRepository subjects,
        IContentScopeRepository scopes,
        IKnowledgeContentRepository contents,
        IKnowledgePathRepository paths,
        IContentEngagementJourneyRepository journeys,
        IClaimRepository claims,
        IClaimCountryVersionRepository claimVersions,
        IReferenceDataCatalogReader? catalog = null)
    {
        _sender = sender;
        _sets = sets;
        _revisions = revisions;
        _templates = templates;
        _subjects = subjects;
        _scopes = scopes;
        _contents = contents;
        _paths = paths;
        _journeys = journeys;
        _claims = claims;
        _claimVersions = claimVersions;
        _catalog = catalog;
    }

    // ================================================================ produce

    public async Task<ContentSetReleaseProduction> ProduceAsync(
        Guid tenantId, ContentSetRevision revision, ContentSetRenderedArtifact artifact, CancellationToken cancellationToken)
    {
        var production = new ContentSetReleaseProduction();

        // Idempotent: a revision that already carries its outputs never produces a second pair.
        if (revision.ProducedKnowledgeContentId is { } producedContent && revision.ProducedKnowledgePathId is { } producedPath)
        {
            production.ContentId = producedContent;
            production.ContentCode = revision.ProducedKnowledgeContentCode;
            production.PathId = producedPath;
            production.PathCode = revision.ProducedKnowledgePathCode;
            return production;
        }

        // ---------------- preconditions (read-only; nothing is written when one fails) ----------------
        var set = await _sets.GetByIdAsync(tenantId, revision.ContentSetId, cancellationToken);
        if (set is null)
        {
            return production.Fail(ContentSetReleaseErrors.SourceUnavailable,
                "The content set of this revision can no longer be read.", 409);
        }

        if (revision.SelectedComponents.Count == 0)
        {
            return production.Fail(ContentSetReleaseErrors.ComponentNotPublished,
                "A content set needs at least one component to be released.", 409);
        }

        var contents = new Dictionary<Guid, KnowledgeContent>();
        var notPublished = new List<string>();
        foreach (var component in revision.SelectedComponents)
        {
            var content = await _contents.GetByIdAsync(tenantId, component.KnowledgeContentId, cancellationToken);
            if (content is null || content.IsArchived()
                || !string.Equals(content.ContentStatus, KnowledgeContentStatuses.Published, StringComparison.OrdinalIgnoreCase))
            {
                notPublished.Add(content?.ContentCode ?? component.KnowledgeContentId.ToString("D"));
                continue;
            }

            contents[component.SelectionId] = content;
        }

        if (notPublished.Count > 0)
        {
            return production.Fail(ContentSetReleaseErrors.ComponentNotPublished,
                $"Every component must be published before release; not published: {string.Join(", ", notPublished.Distinct())}.",
                409);
        }

        // The pinned (frozen) component language is authoritative; one language only — never guessed.
        var languages = revision.SelectedComponents
            .Select(c => c.LanguageCode?.Trim() ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (languages.Count != 1)
        {
            var detail = string.Join(", ", revision.SelectedComponents
                .Select(c => $"{contents[c.SelectionId].ContentCode}={(string.IsNullOrWhiteSpace(c.LanguageCode) ? "?" : c.LanguageCode.Trim())}")
                .Distinct());
            return production.Fail(ContentSetReleaseErrors.ComponentLanguageMixed,
                $"The components are not in one language ({detail}); release needs a single-language set.", 409);
        }

        var language = languages[0];

        var template = await _templates.GetByIdAsync(tenantId, revision.Template.ConceptChainTemplateId, cancellationToken);
        if (template is null)
        {
            return production.Fail(ContentSetReleaseErrors.SourceUnavailable,
                "The pinned composition template of this revision can no longer be read.", 409);
        }

        var subject = await _subjects.GetByIdAsync(tenantId, template.SubjectId, cancellationToken);
        var productId = PrimaryGlobalProduct(subject);
        Guid? audienceId = template.ForWhomAudienceProfileIds.Count == 1 ? template.ForWhomAudienceProfileIds[0] : null;

        var (claimInputs, claimError) = await BuildClaimRefsAsync(tenantId, revision, cancellationToken);
        if (claimError is not null)
        {
            return production.Fail(claimError.Value.Errors, claimError.Value.Status);
        }

        // WP-CL-BE-6 gate, checked BEFORE the first write so an unusable claim leaves nothing behind (the create
        // command re-runs the very same checks).
        var (resolvedRefs, resolveFailure) = await KnowledgeContentClaimLinks.ResolveAsync(
            claimInputs, tenantId, productId, _claims, _claimVersions, cancellationToken);
        if (resolveFailure is not null)
        {
            return production.Fail(new[] { resolveFailure.Code, resolveFailure.Message }, resolveFailure.StatusCode);
        }

        if (await KnowledgeContentClaimLinks.CheckPublishAsync(
                resolvedRefs!, language, tenantId, _claims, _claimVersions, cancellationToken) is { } gate)
        {
            return production.Fail(new[] { gate.Code, gate.Message }, gate.StatusCode);
        }

        var ordered = ContentSetPathOrder.Order(template, revision.SelectedComponents);
        var previous = await PreviousOutputsAsync(tenantId, revision, cancellationToken);

        // ---------------- writes (compensated on failure) ----------------
        var origin = new KnowledgeStudioOrigin
        {
            ContentSetId = revision.ContentSetId,
            ContentSetRevisionId = revision.Id,
            ConceptChainTemplateId = revision.Template.ConceptChainTemplateId,
            ChainVersion = revision.Template.ChainVersion
        };
        var now = DateTimeOffset.UtcNow;
        var version = revision.RevisionNumber.ToString(CultureInfo.InvariantCulture);
        var pathCreated = false;

        try
        {
            // A — the assembled presentation.
            var contentCode = await FreeContentCodeAsync(tenantId, $"KC-{set.SetCode}", revision.RevisionNumber, cancellationToken);
            var created = await _sender.Send(new CreateKnowledgeContentCommand(
                contentCode, set.SetName, KnowledgeContentTypes.AssembledPresentation, template.SubjectId, language, version,
                now,
                ContentStatus: KnowledgeContentStatuses.Published,
                AudienceProfileId: audienceId,
                ProductId: productId,
                Summary: set.Description,
                ContentAssetRef: artifact.ContentId.ToString("D"),
                Source: KnowledgeContentSources.ContentStudio,
                ClaimRefs: claimInputs,
                StudioOrigin: origin), cancellationToken);
            if (!created.IsSuccessful)
            {
                return production.Fail(created.Errors ?? new[] { "Content could not be created." }, created.StatusCode);
            }

            production.ContentId = created.Data;
            production.ContentCode = contentCode;

            // D — the chain-ordered path: a new version of the previous release's path when that one is published,
            // else a new path (same code when an earlier, no longer published, release left one).
            var objective = Truncate(string.IsNullOrWhiteSpace(set.Description) ? set.SetName : set.Description!, 500);
            var pathName = Truncate(set.SetName, 200);
            var previousPath = previous.PathId is { } pp ? await _paths.GetByIdAsync(tenantId, pp, cancellationToken) : null;
            Guid pathId;
            string pathCode;
            if (previousPath is not null && previousPath.IsPublished() && !previousPath.IsArchived())
            {
                var opened = await _sender.Send(
                    new CreateKnowledgePathVersionCommand(previousPath.Id, version, origin), cancellationToken);
                if (!opened.IsSuccessful)
                {
                    return await FailCompensatedAsync(tenantId, production, pathCreated, opened.Errors, opened.StatusCode, cancellationToken);
                }

                pathId = opened.Data;
                pathCode = previousPath.PathCode;
                pathCreated = true;

                // The clone carries the previous step set; retire it (the new steps are the new revision's).
                var draft = await _paths.GetByIdAsync(tenantId, pathId, cancellationToken);
                foreach (var step in draft?.ActiveSteps().ToList() ?? new List<KnowledgePathStep>())
                {
                    var archived = await _sender.Send(new ArchiveKnowledgePathStepCommand(pathId, step.StepId), cancellationToken);
                    if (!archived.IsSuccessful)
                    {
                        production.PathId = pathId;
                        return await FailCompensatedAsync(tenantId, production, pathCreated, archived.Errors, archived.StatusCode, cancellationToken);
                    }
                }

                var updated = await _sender.Send(new UpdateKnowledgePathCommand(
                    pathId, pathName, template.SubjectId, objective, version, now,
                    Description: set.Description, AudienceProfileId: audienceId, LanguageCode: language,
                    PathStatus: KnowledgePathStatuses.Draft, Source: KnowledgePathSources.ContentStudio), cancellationToken);
                if (!updated.IsSuccessful)
                {
                    production.PathId = pathId;
                    return await FailCompensatedAsync(tenantId, production, pathCreated, updated.Errors, updated.StatusCode, cancellationToken);
                }
            }
            else
            {
                pathCode = previousPath?.PathCode ?? await FreePathCodeAsync(tenantId, $"KP-{set.SetCode}", revision.RevisionNumber, cancellationToken);
                var createdPath = await _sender.Send(new CreateKnowledgePathCommand(
                    pathCode, pathName, template.SubjectId, objective, version, now,
                    Description: set.Description, AudienceProfileId: audienceId, LanguageCode: language,
                    PathStatus: KnowledgePathStatuses.Draft, Source: KnowledgePathSources.ContentStudio,
                    StudioOrigin: origin), cancellationToken);
                if (!createdPath.IsSuccessful)
                {
                    return await FailCompensatedAsync(tenantId, production, pathCreated, createdPath.Errors, createdPath.StatusCode, cancellationToken);
                }

                pathId = createdPath.Data;
                pathCreated = true;
            }

            production.PathId = pathId;
            production.PathCode = pathCode;

            var order = 0;
            foreach (var component in ordered)
            {
                order++;
                var content = contents[component.SelectionId];
                var added = await _sender.Send(new AddKnowledgePathStepCommand(
                    pathId, order * 10, $"S{order:000}", Truncate(content.ContentTitle, 300), StepTypeFor(content.ContentType),
                    content.Id, IsRequired: true, ConceptNodeId: content.ConceptNodeId), cancellationToken);
                if (!added.IsSuccessful)
                {
                    return await FailCompensatedAsync(tenantId, production, pathCreated, added.Errors, added.StatusCode, cancellationToken);
                }
            }

            // Supersede the previous path BEFORE publishing (V-P10: two published versions of a code may not overlap).
            if (previousPath is not null && previousPath.IsPublished() && !previousPath.IsArchived())
            {
                if (await IsPathUsedByPublishedJourneyAsync(tenantId, previousPath, cancellationToken))
                {
                    production.Warnings.Add(ContentSetReleaseWarnings.PreviousPathInUse);
                }

                var retired = await _sender.Send(PathUpdate(previousPath, KnowledgePathStatuses.Inactive), cancellationToken);
                if (!retired.IsSuccessful)
                {
                    return await FailCompensatedAsync(tenantId, production, pathCreated, retired.Errors, retired.StatusCode, cancellationToken);
                }

                production.DeactivatedPreviousPathId = previousPath.Id;
            }

            var published = await _sender.Send(new PublishKnowledgePathCommand(pathId), cancellationToken);
            if (!published.IsSuccessful)
            {
                return await FailCompensatedAsync(tenantId, production, pathCreated, published.Errors, published.StatusCode, cancellationToken);
            }

            // Supersede the previous assembled presentation last (its restore is the cheapest compensation).
            var previousContent = previous.ContentId is { } pc
                ? await _contents.GetByIdAsync(tenantId, pc, cancellationToken)
                : null;
            if (previousContent is not null && !previousContent.IsArchived()
                && string.Equals(previousContent.ContentStatus, KnowledgeContentStatuses.Published, StringComparison.OrdinalIgnoreCase))
            {
                var retiredContent = await _sender.Send(
                    ContentUpdate(previousContent, KnowledgeContentStatuses.Inactive), cancellationToken);
                if (!retiredContent.IsSuccessful)
                {
                    return await FailCompensatedAsync(tenantId, production, pathCreated, retiredContent.Errors, retiredContent.StatusCode, cancellationToken);
                }

                production.DeactivatedPreviousContentId = previousContent.Id;
            }

            return production;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await SafeCompensateAsync(tenantId, production, pathCreated, CancellationToken.None);
            throw;
        }
    }

    public Task CompensateAsync(Guid tenantId, ContentSetReleaseProduction production, CancellationToken cancellationToken)
        => SafeCompensateAsync(tenantId, production, production.PathId is not null, cancellationToken);

    private async Task<ContentSetReleaseProduction> FailCompensatedAsync(
        Guid tenantId, ContentSetReleaseProduction production, bool pathCreated, IReadOnlyList<string>? errors, int status,
        CancellationToken cancellationToken)
    {
        await SafeCompensateAsync(tenantId, production, pathCreated, cancellationToken);
        return production.Fail(errors ?? new[] { "Release production failed." }, status);
    }

    /// <summary>Reverse order of the writes: restore the superseded content / path, then archive what was created.
    /// Best effort — a compensation step never masks the original failure.</summary>
    private async Task SafeCompensateAsync(
        Guid tenantId, ContentSetReleaseProduction production, bool pathCreated, CancellationToken cancellationToken)
    {
        await TryAsync(async () =>
        {
            if (production.DeactivatedPreviousContentId is { } pc
                && await _contents.GetByIdAsync(tenantId, pc, cancellationToken) is { } content)
            {
                await _sender.Send(ContentUpdate(content, KnowledgeContentStatuses.Published), cancellationToken);
            }
        });
        await TryAsync(async () =>
        {
            if (pathCreated && production.PathId is { } createdPath)
            {
                await _sender.Send(new ArchiveKnowledgePathCommand(createdPath), cancellationToken);
            }
        });
        await TryAsync(async () =>
        {
            if (production.DeactivatedPreviousPathId is { } pp)
            {
                await _sender.Send(new PublishKnowledgePathCommand(pp), cancellationToken);
            }
        });
        await TryAsync(async () =>
        {
            if (production.ContentId is { } createdContent)
            {
                await _sender.Send(new ArchiveKnowledgeContentCommand(createdContent), cancellationToken);
            }
        });
    }

    private static async Task TryAsync(Func<Task> step)
    {
        try
        {
            await step();
        }
        catch
        {
            // Compensation is best effort; the caller already reports the original failure.
        }
    }

    // ================================================================ retire (withdrawal)

    public async Task<ContentSetReleaseProduction> RetireAsync(
        Guid tenantId, ContentSetRevision revision, CancellationToken cancellationToken)
    {
        var outcome = new ContentSetReleaseProduction
        {
            ContentId = revision.ProducedKnowledgeContentId,
            ContentCode = revision.ProducedKnowledgeContentCode,
            PathId = revision.ProducedKnowledgePathId,
            PathCode = revision.ProducedKnowledgePathCode
        };

        KnowledgeContent? retiredContent = null;
        string? retiredContentStatus = null;
        if (revision.ProducedKnowledgeContentId is { } contentId
            && await _contents.GetByIdAsync(tenantId, contentId, cancellationToken) is { } content
            && !content.IsArchived()
            && !string.Equals(content.ContentStatus, KnowledgeContentStatuses.Inactive, StringComparison.OrdinalIgnoreCase))
        {
            retiredContentStatus = content.ContentStatus;
            var result = await _sender.Send(ContentUpdate(content, KnowledgeContentStatuses.Inactive), cancellationToken);
            if (!result.IsSuccessful)
            {
                return outcome.Fail(result.Errors ?? new[] { "The produced content could not be retired." }, result.StatusCode);
            }

            retiredContent = content;
        }

        if (revision.ProducedKnowledgePathId is { } pathId
            && await _paths.GetByIdAsync(tenantId, pathId, cancellationToken) is { } path
            && !path.IsArchived()
            && !string.Equals(path.PathStatus, KnowledgePathStatuses.Inactive, StringComparison.OrdinalIgnoreCase))
        {
            if (await IsPathUsedByPublishedJourneyAsync(tenantId, path, cancellationToken))
            {
                outcome.Warnings.Add(ContentSetReleaseWarnings.PathInUse);
                return outcome;
            }

            var result = await _sender.Send(PathUpdate(path, KnowledgePathStatuses.Inactive), cancellationToken);
            if (!result.IsSuccessful)
            {
                if (retiredContent is not null && retiredContentStatus is not null)
                {
                    var reloaded = await _contents.GetByIdAsync(tenantId, retiredContent.Id, cancellationToken);
                    await TryAsync(() => reloaded is null
                        ? Task.CompletedTask
                        : _sender.Send(ContentUpdate(reloaded, retiredContentStatus), cancellationToken));
                }

                return outcome.Fail(result.Errors ?? new[] { "The produced path could not be retired." }, result.StatusCode);
            }
        }

        return outcome;
    }

    // ================================================================ helpers

    /// <summary>A published, non-archived journey with an active stage on this path: pinned to this version, or
    /// following the path code (<c>latest-published</c>, counted conservatively).</summary>
    private async Task<bool> IsPathUsedByPublishedJourneyAsync(Guid tenantId, KnowledgePath path, CancellationToken ct)
        => (await _journeys.ListAsync(tenantId, ct)).Any(j =>
            j.IsPublished() && !j.IsArchived()
            && j.ActiveStages().Any(s =>
                s.RecommendedKnowledgePathId == path.Id
                || (string.Equals(s.PathVersionPinPolicy, ContentEngagementJourneyPathPin.LatestPublished, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(s.PathCode, path.PathCode, StringComparison.OrdinalIgnoreCase))));

    /// <summary>The newest OTHER revision of the same set that produced outputs (the lineage a re-release supersedes).</summary>
    private async Task<(Guid? ContentId, Guid? PathId)> PreviousOutputsAsync(
        Guid tenantId, ContentSetRevision revision, CancellationToken ct)
    {
        var previous = (await _revisions.ListByContentSetAsync(tenantId, revision.ContentSetId, ct))
            .Where(r => r.Id != revision.Id && (r.ProducedKnowledgeContentId is not null || r.ProducedKnowledgePathId is not null))
            .OrderByDescending(r => r.RevisionNumber)
            .FirstOrDefault();
        return (previous?.ProducedKnowledgeContentId, previous?.ProducedKnowledgePathId);
    }

    /// <summary>Claim refs from the frozen SelectedClaims (one per claim record). A scope whose MarketRefs name exactly
    /// ONE COUNTRY_CODES country binds that country's version; otherwise the core claim. The country version is the one
    /// the coverage cell shows (approved › review-required › in-review › draft); none ⇒ 409 claim_not_approved.</summary>
    private async Task<(List<KnowledgeContentClaimRefInput> Refs, (IReadOnlyList<string> Errors, int Status)? Error)>
        BuildClaimRefsAsync(Guid tenantId, ContentSetRevision revision, CancellationToken ct)
    {
        var refs = new List<KnowledgeContentClaimRefInput>();
        var claimIds = revision.SelectedClaims.Select(c => c.ClaimId).Distinct().ToList();
        if (claimIds.Count == 0)
        {
            return (refs, null);
        }

        var (country, countryError) = await ScopeCountryAsync(tenantId, revision, ct);
        if (countryError is not null)
        {
            return (refs, countryError);
        }

        foreach (var claimId in claimIds)
        {
            var claim = await _claims.GetByIdAsync(tenantId, claimId, ct);
            if (claim is null)
            {
                return (refs, (new[] { KnowledgeContentClaimErrors.ClaimNotFound,
                    $"A selected claim ({claimId:D}) does not exist in this tenant." }, 409));
            }

            if (country is null)
            {
                refs.Add(new KnowledgeContentClaimRefInput(claim.ClaimCode, claim.Id));
                continue;
            }

            var version = ClaimLines.CellVersion(
                (await _claimVersions.ListByClaimCodeAsync(tenantId, claim.ClaimCode, ct)).Where(v => v.ClaimId == claim.Id),
                country);
            if (version is null)
            {
                return (refs, (new[] { KnowledgeContentClaimErrors.ClaimNotApproved,
                    $"Claim '{claim.ClaimCode}' ({country}) has no version in that country; the set cannot be released." }, 409));
            }

            refs.Add(new KnowledgeContentClaimRefInput(claim.ClaimCode, claim.Id, version.Id, country));
        }

        return (refs, null);
    }

    /// <summary>The single COUNTRY_CODES country among the scope's MarketRefs, or null (no scope / no or several countries
    /// ⇒ core refs). The catalog is needed only when there are MarketRefs to classify; unreadable ⇒ 503 (fail-closed).</summary>
    private async Task<(string? Country, (IReadOnlyList<string> Errors, int Status)? Error)> ScopeCountryAsync(
        Guid tenantId, ContentSetRevision revision, CancellationToken ct)
    {
        if (revision.Scope is not { } scopeRef)
        {
            return (null, null);
        }

        var scope = await _scopes.GetByIdAsync(tenantId, scopeRef.ContentScopeId, ct);
        var markets = scope?.MarketRefs
            .Select(m => m?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(m => m.Length > 0)
            .Distinct()
            .ToList() ?? new List<string>();
        if (markets.Count == 0)
        {
            return (null, null);
        }

        ReferenceSetSnapshot? set = null;
        try
        {
            set = _catalog is null ? null : await _catalog.GetPublishedValuesAsync(ClaimReferenceSets.CountryCodes, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            set = null;
        }

        if (set is null || !set.IsPublished)
        {
            return (null, (new[] { KnowledgeContentClaimErrors.DependencyUnavailable,
                $"Reference set '{ClaimReferenceSets.CountryCodes}' is unavailable; the scope market cannot be resolved." }, 503));
        }

        var known = set.Values.Where(v => v.IsActive && !v.IsDeprecated)
            .Select(v => v.ValueCode.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
        var countries = markets.Where(known.Contains).ToList();
        return (countries.Count == 1 ? countries[0] : null, null);
    }

    private static Guid? PrimaryGlobalProduct(Subject? subject)
        => subject?.ExternalReferences
            .Where(r => r.IsPrimary
                && string.Equals(r.SourceSystem?.Trim(), GlobalProductSourceSystem, StringComparison.OrdinalIgnoreCase))
            .Select(r => Guid.TryParse(r.ExternalId, out var id) && id != Guid.Empty ? id : (Guid?)null)
            .FirstOrDefault(id => id is not null);

    /// <summary>Path step type from the component content type (the step vocabulary has no "presentation"; the core
    /// message is the neutral default).</summary>
    internal static string StepTypeFor(string? contentType) => KnowledgeContentTypes.Normalize(contentType) switch
    {
        KnowledgeContentTypes.Quiz => KnowledgePathStepTypes.Quiz,
        KnowledgeContentTypes.Faq => KnowledgePathStepTypes.Faq,
        KnowledgeContentTypes.ObjectionHandling => KnowledgePathStepTypes.ObjectionHandling,
        KnowledgeContentTypes.ClinicalSummary => KnowledgePathStepTypes.ClinicalEvidence,
        KnowledgeContentTypes.Lesson or KnowledgeContentTypes.TrainingMaterial => KnowledgePathStepTypes.Lesson,
        _ => KnowledgePathStepTypes.CoreMessage
    };

    private async Task<string> FreeContentCodeAsync(Guid tenantId, string baseCode, int revisionNumber, CancellationToken ct)
    {
        if (await _contents.GetActiveByCodeAsync(tenantId, baseCode, ct) is null)
        {
            return baseCode;
        }

        var candidate = $"{baseCode}-R{revisionNumber}";
        for (var n = 2; await _contents.GetActiveByCodeAsync(tenantId, candidate, ct) is not null; n++)
        {
            candidate = $"{baseCode}-R{revisionNumber}-{n}";
        }

        return candidate;
    }

    private async Task<string> FreePathCodeAsync(Guid tenantId, string baseCode, int revisionNumber, CancellationToken ct)
    {
        async Task<bool> Taken(string code) => (await _paths.ListByCodeAsync(tenantId, code, ct)).Any(p => !p.IsArchived());

        if (!await Taken(baseCode))
        {
            return baseCode;
        }

        var candidate = $"{baseCode}-R{revisionNumber}";
        for (var n = 2; await Taken(candidate); n++)
        {
            candidate = $"{baseCode}-R{revisionNumber}-{n}";
        }

        return candidate;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    /// <summary>A full-replace update that only changes the status (the Update command has no partial form).</summary>
    private static UpdateKnowledgeContentCommand ContentUpdate(KnowledgeContent c, string status) => new(
        c.Id, c.ContentTitle, c.ContentType, c.SubjectId, c.LanguageCode, c.ContentVersion, c.EffectiveFrom,
        ContentStatus: status,
        TopicId: c.TopicId, AudienceProfileId: c.AudienceProfileId, ConceptNodeId: c.ConceptNodeId, BrandId: c.BrandId,
        ProductId: c.ProductId, CampaignId: c.CampaignId, SegmentId: c.SegmentId, Summary: c.Summary,
        ContentBodyRef: c.ContentBodyRef, ContentAssetRef: c.ContentAssetRef, FileRef: c.FileRef, Url: c.Url,
        EffectiveTo: c.EffectiveTo, Source: c.Source, Tags: c.Tags.ToList(),
        ExternalReferences: c.ExternalReferences.Select(r => new KnowledgeExternalReferenceInput(
            r.SourceSystem, r.ExternalId, r.ExternalCode, r.ExternalName, r.ImportedAt, r.IsPrimary)).ToList(),
        ClaimRefs: null);

    /// <summary>A full-replace update that only changes the status (a published version accepts exactly that).</summary>
    private static UpdateKnowledgePathCommand PathUpdate(KnowledgePath p, string status) => new(
        p.Id, p.PathName, p.SubjectId, p.Objective, p.PathVersion, p.EffectiveFrom,
        Description: p.Description, TopicId: p.TopicId, AudienceProfileId: p.AudienceProfileId, LanguageCode: p.LanguageCode,
        PathStatus: status, EffectiveTo: p.EffectiveTo, Source: p.Source);
}
