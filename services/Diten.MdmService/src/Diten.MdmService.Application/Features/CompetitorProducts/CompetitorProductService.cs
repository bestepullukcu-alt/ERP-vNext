using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.MdmService.Application.Features.CompetitorProducts;

/// <summary>
/// WP-VW-W4 (W4-BE-b2, §2.6) — the competitor products: the record (aliases, ingredient tags, our products, indications,
/// country price + reimbursement with its history), the rep proposal, the decision on it (complete / merge / reject)
/// approved through the b1 workflow core, the direct merge of two active records, the merged → survivor read.
/// <list type="bullet">
/// <item><b>Tenant.</b> Every read and write is the caller's tenant (the repository filters it; another tenant's record is
/// 404).</item>
/// <item><b>Uniqueness.</b> Company + name, normalized (Turkish folding), among the records that still count (proposed /
/// active / inactive); 409 <c>competitor_duplicate</c>.</item>
/// <item><b>Direct edits</b> (create, update incl. prices, deactivate, merge two active records) need the manage key and no
/// approval (K-W6 decision 2); every change is in the change log, every price change opens a new period.</item>
/// <item><b>Proposals</b> are visible to their proposer only (<c>includeProposed=mine</c>) and to the managers.</item>
/// </list>
/// </summary>
public sealed class CompetitorProductService
{
    private const int DefaultPageSize = 20;

    private readonly ICompetitorProductRepository _products;
    private readonly ICompetitorProposalDecisionRepository _decisions;
    private readonly IWorkflowApprovalStore<CompetitorProposalDecision> _decisionStore;
    private readonly ITenantContext _tenant;
    private readonly IMdmActorContext _actor;
    private readonly IMdmPermissionContext _permissions;
    private readonly IVerifiedMarketReferenceResolver _markets;
    private readonly IMdmReferenceValueReader _referenceSets;
    private readonly WorkflowApprovalFlow _flow;
    private readonly WorkflowApprovalReconciler _reconciler;
    private readonly IMdmApprovalSettings _settings;

    public CompetitorProductService(
        ICompetitorProductRepository products,
        ICompetitorProposalDecisionRepository decisions,
        IWorkflowApprovalStore<CompetitorProposalDecision> decisionStore,
        ITenantContext tenant,
        IMdmActorContext actor,
        IMdmPermissionContext permissions,
        IVerifiedMarketReferenceResolver markets,
        IMdmReferenceValueReader referenceSets,
        WorkflowApprovalFlow flow,
        WorkflowApprovalReconciler reconciler,
        IMdmApprovalSettings settings)
    {
        _products = products;
        _decisions = decisions;
        _decisionStore = decisionStore;
        _tenant = tenant;
        _actor = actor;
        _permissions = permissions;
        _markets = markets;
        _referenceSets = referenceSets;
        _flow = flow;
        _reconciler = reconciler;
        _settings = settings;
    }

    private Guid TenantId => _tenant.TenantId;
    private string? Actor => _actor.ActorName;
    private bool CanManage => _permissions.Has(CompetitorPermissions.Manage);

    /// <summary>A record that blocks a second one with the same company + name (a merged / rejected one does not).</summary>
    public static bool CountsForUniqueness(CompetitorProduct r)
        => r.Status is CompetitorProductStatuses.Proposed or CompetitorProductStatuses.Active or CompetitorProductStatuses.Inactive;

    // ═════════════════════════ reads ═════════════════════════

    public async Task<Response<CompetitorListDto>> ListAsync(Guid? productId, string? company, string? ingredient, string? market,
        string? reimbursement, string? status, string? q, string? includeProposed, int? page, int? pageSize, CancellationToken ct)
    {
        var all = await _products.ListAsync(TenantId, ct);
        var statuses = string.IsNullOrWhiteSpace(status)
            ? new HashSet<string> { CompetitorProductStatuses.Active, CompetitorProductStatuses.Inactive }
            : status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant()).Where(s => s != CompetitorProductStatuses.Proposed).ToHashSet();
        var mine = string.Equals(includeProposed, "mine", StringComparison.OrdinalIgnoreCase);
        var actor = Actor;
        var folded = CompetitorNames.Fold(q);

        var rows = all.Where(r => statuses.Contains(r.Status) || (mine && IsMyProposal(r, actor)))
            .Where(r => productId is null || r.CompetesWithGlobalProductIds.Contains(productId.Value))
            .Where(r => string.IsNullOrWhiteSpace(company) || CompetitorNames.Fold(r.Company).Contains(CompetitorNames.Fold(company), StringComparison.Ordinal))
            .Where(r => string.IsNullOrWhiteSpace(ingredient) || r.ActiveIngredients.Any(i => CompetitorNames.Fold(i).Contains(CompetitorNames.Fold(ingredient), StringComparison.Ordinal)))
            .Where(r => string.IsNullOrWhiteSpace(market) || r.CountryPrices.Any(p => string.Equals(p.MarketCode, market.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Where(r => string.IsNullOrWhiteSpace(reimbursement) || r.CountryPrices.Any(p => string.Equals(p.ReimbursementStatusCode, reimbursement.Trim(), StringComparison.OrdinalIgnoreCase)
                                                                                         && (string.IsNullOrWhiteSpace(market) || string.Equals(p.MarketCode, market.Trim(), StringComparison.OrdinalIgnoreCase))))
            .Where(r => folded.Length == 0 || r.Aliases.Prepend(r.Name).Any(n => CompetitorNames.Fold(n).Contains(folded, StringComparison.Ordinal)))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, 100);
        var number = Math.Max(page ?? 1, 1);
        return Response<CompetitorListDto>.Success(new CompetitorListDto(
            rows.Skip((number - 1) * size).Take(size).Select(r => CompetitorProductDto.From(r)).ToList(), rows.Count, number, size));
    }

    /// <summary>One record. A merged record names its survivor (followed through a chain of merges); another rep's proposal
    /// is not shown to a rep (404, like another tenant's record).</summary>
    public async Task<Response<CompetitorProductDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var record = await _products.GetAsync(TenantId, id, ct);
        if (record is null || (record.Status == CompetitorProductStatuses.Proposed && !CanManage && !IsMyProposal(record, Actor)))
        {
            return CompetitorErrors.Fail<CompetitorProductDto>(CompetitorErrors.NotFound, "Competitor product not found.", 404);
        }

        return Response<CompetitorProductDto>.Success(CompetitorProductDto.From(record, await SurvivorAsync(record, ct)));
    }

    /// <summary>The report's short list: active records (and the caller's own proposals) for one of our products / an
    /// indication / a text, with the market card of <paramref name="market"/>. An inactive record is never offered.</summary>
    public async Task<Response<IReadOnlyList<CompetitorLookupDto>>> LookupAsync(Guid? productId, Guid? indicationNodeId, string? market,
        string? q, CancellationToken ct)
    {
        var actor = Actor;
        var folded = CompetitorNames.Fold(q);
        var rows = (await _products.ListAsync(TenantId, ct))
            .Where(r => r.Status == CompetitorProductStatuses.Active || IsMyProposal(r, actor))
            .Where(r => productId is null || r.CompetesWithGlobalProductIds.Contains(productId.Value))
            .Where(r => indicationNodeId is null || r.IndicationNodeIds.Count == 0 || r.IndicationNodeIds.Contains(indicationNodeId.Value))
            .Select(r => (Record: r, Score: folded.Length == 0 ? 100 : Math.Max(
                r.Aliases.Prepend(r.Name).Any(n => CompetitorNames.Fold(n).Contains(folded, StringComparison.Ordinal)) ? 100 : 0,
                CompetitorNames.BestSimilarity(q, r))))
            .Where(x => x.Score >= 60)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Record.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(50)
            .Select(x => new CompetitorLookupDto(x.Record.Id, x.Record.Name, x.Record.Company, x.Record.ActiveIngredients,
                x.Record.FormCode, x.Record.Strength, x.Record.Status, IsMyProposal(x.Record, actor),
                string.IsNullOrWhiteSpace(market) ? null
                    : x.Record.CountryPrices.FirstOrDefault(p => string.Equals(p.MarketCode, market.Trim(), StringComparison.OrdinalIgnoreCase))))
            .ToList();
        return Response<IReadOnlyList<CompetitorLookupDto>>.Success(rows);
    }

    /// <summary>Merge targets for a source (or a text): active records only, by name similarity (aliases included).</summary>
    public async Task<Response<IReadOnlyList<CompetitorSimilarDto>>> MergeCandidatesAsync(Guid? sourceId, string? q, CancellationToken ct)
    {
        var all = await _products.ListAsync(TenantId, ct);
        var source = sourceId is { } sid ? all.FirstOrDefault(r => r.Id == sid) : null;
        if (sourceId is not null && source is null)
        {
            return CompetitorErrors.Fail<IReadOnlyList<CompetitorSimilarDto>>(CompetitorErrors.NotFound, "Competitor product not found.", 404);
        }

        var probe = string.IsNullOrWhiteSpace(q) ? source?.Name : q;
        var rows = all.Where(r => r.IsSelectable && r.Id != sourceId)
            .Select(r => Similar(r, probe, source))
            .OrderByDescending(s => s.Score).ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(20)
            .ToList();
        return Response<IReadOnlyList<CompetitorSimilarDto>>.Success(rows);
    }

    /// <summary>G2 — the report usage comes from the CRM (W4-BE-a aggregate read, joined by WEB-c); the MDM never calls the
    /// CRM: 0 / not available here.</summary>
    public async Task<Response<CompetitorUsageDto>> UsageAsync(Guid id, CancellationToken ct)
        => await _products.GetAsync(TenantId, id, ct) is null
            ? CompetitorErrors.Fail<CompetitorUsageDto>(CompetitorErrors.NotFound, "Competitor product not found.", 404)
            : Response<CompetitorUsageDto>.Success(new CompetitorUsageDto(id, 0, 0, false, "crm_usage_pending"));

    // ═════════════════════════ direct writes (manage, no approval) ═════════════════════════

    public async Task<Response<Guid>> CreateAsync(CompetitorProductWriteRequest request, CancellationToken ct)
    {
        var fields = CompetitorValidation.Normalize(request.ToFields());
        if (await CheckAsync<Guid>(fields, null, ct) is { } failure)
        {
            return failure;
        }

        var now = DateTime.UtcNow;
        var record = new CompetitorProduct { TenantId = TenantId, Status = CompetitorProductStatuses.Active, CreatedBy = Actor, Version = 0 };
        ApplyFields(record, fields, Actor, now, history: false);
        record.ChangeLog.Add(new CompetitorChange { At = now, By = Actor, Field = "created", NewValue = record.Name });
        await _products.InsertAsync(record, ct);
        return Response<Guid>.Success(record.Id, 201);
    }

    public async Task<Response<CompetitorProductDto>> UpdateAsync(Guid id, CompetitorProductWriteRequest request, CancellationToken ct)
    {
        var record = await _products.GetAsync(TenantId, id, ct);
        if (record is null)
        {
            return CompetitorErrors.Fail<CompetitorProductDto>(CompetitorErrors.NotFound, "Competitor product not found.", 404);
        }

        if (record.Status is not (CompetitorProductStatuses.Active or CompetitorProductStatuses.Inactive))
        {
            return CompetitorErrors.Fail<CompetitorProductDto>(CompetitorErrors.NotEditable,
                record.Status == CompetitorProductStatuses.Proposed
                    ? "A proposal changes through its decision (complete / merge / reject), not by a direct edit."
                    : "A merged or rejected record is a trace and never changes.", 409);
        }

        if (request.ExpectedVersion is { } ev && ev != record.Version)
        {
            return Conflict<CompetitorProductDto>();
        }

        var fields = CompetitorValidation.Normalize(request.ToFields());
        if (await CheckAsync<CompetitorProductDto>(fields, record.Id, ct) is { } failure)
        {
            return failure;
        }

        var expected = record.Version;
        ApplyFields(record, fields, Actor, DateTime.UtcNow, history: true);
        return await _products.ReplaceAsync(record, expected, ct)
            ? Response<CompetitorProductDto>.Success(CompetitorProductDto.From(record))
            : Conflict<CompetitorProductDto>();
    }

    public async Task<Response<CompetitorProductDto>> DeactivateAsync(Guid id, int? expectedVersion, CancellationToken ct)
    {
        var record = await _products.GetAsync(TenantId, id, ct);
        if (record is null)
        {
            return CompetitorErrors.Fail<CompetitorProductDto>(CompetitorErrors.NotFound, "Competitor product not found.", 404);
        }

        if (record.Status != CompetitorProductStatuses.Active)
        {
            return CompetitorErrors.Fail<CompetitorProductDto>(CompetitorErrors.NotEditable, "Only an active record is deactivated.", 409);
        }

        if (expectedVersion is { } ev && ev != record.Version)
        {
            return Conflict<CompetitorProductDto>();
        }

        var now = DateTime.UtcNow;
        var expected = record.Version;
        record.ChangeLog.Add(new CompetitorChange { At = now, By = Actor, Field = "status", OldValue = record.Status, NewValue = CompetitorProductStatuses.Inactive });
        record.Status = CompetitorProductStatuses.Inactive;
        record.DeactivatedAt = now;
        record.DeactivatedBy = Actor;
        record.UpdatedBy = Actor;
        return await _products.ReplaceAsync(record, expected, ct)
            ? Response<CompetitorProductDto>.Success(CompetitorProductDto.From(record))
            : Conflict<CompetitorProductDto>();
    }

    /// <summary>R3 "Başka kayıtla birleştir" — two ACTIVE records, directly (no approval, G1); the caller confirms that it is
    /// irreversible.</summary>
    public async Task<Response<CompetitorProductDto>> MergeAsync(Guid id, CompetitorMergeRequest request, CancellationToken ct)
    {
        if (!request.Confirm)
        {
            return CompetitorErrors.Fail<CompetitorProductDto>(CompetitorErrors.MergeConfirmRequired, "A merge is irreversible: confirm it.", 400);
        }

        if (request.TargetId == id)
        {
            return CompetitorErrors.Fail<CompetitorProductDto>(CompetitorErrors.MergeSelf, "A record cannot be merged into itself.", 400);
        }

        var source = await _products.GetAsync(TenantId, id, ct);
        var target = await _products.GetAsync(TenantId, request.TargetId, ct);
        if (source is null || target is null)
        {
            return CompetitorErrors.Fail<CompetitorProductDto>(CompetitorErrors.NotFound, "Competitor product not found.", 404);
        }

        if (!source.IsSelectable || !target.IsSelectable)
        {
            return CompetitorErrors.Fail<CompetitorProductDto>(CompetitorErrors.NotSelectable,
                "Both records must be active (a proposal is merged through its decision).", 409);
        }

        if (request.ExpectedVersion is { } ev && ev != source.Version)
        {
            return Conflict<CompetitorProductDto>();
        }

        var sourceVersion = source.Version;
        var targetVersion = target.Version;
        CompetitorMerge.Apply(source, target, request.MoveMarketCodes, Actor, DateTime.UtcNow, "record");
        if (!await _products.ReplaceAsync(target, targetVersion, ct) || !await _products.ReplaceAsync(source, sourceVersion, ct))
        {
            return Conflict<CompetitorProductDto>();
        }

        return Response<CompetitorProductDto>.Success(CompetitorProductDto.From(target));
    }

    // ═════════════════════════ the rep proposal (propose) ═════════════════════════

    /// <summary>A rep's "not in the list" from a report: <c>proposed</c>, usable at once by its proposer. A name + company
    /// that already exists returns THAT record (<c>existing</c>) — a merged one, its survivor.</summary>
    public async Task<Response<CompetitorProposalResult>> ProposeAsync(CompetitorProposalRequest request, CancellationToken ct)
    {
        var name = CompetitorValidation.Clean(request.Name);
        var company = CompetitorValidation.Clean(request.Company) ?? string.Empty;
        if (name is null || name.Length > CompetitorLimits.Name || company.Length > CompetitorLimits.Company
            || request.ProductId is null || request.ProductId == Guid.Empty)
        {
            return CompetitorErrors.Fail<CompetitorProposalResult>(CompetitorErrors.Invalid,
                $"A proposal needs a name (≤ {CompetitorLimits.Name}), an optional company (≤ {CompetitorLimits.Company}) and our product.", 400);
        }

        var key = CompetitorNames.Key(company, name);
        var all = await _products.ListAsync(TenantId, ct);
        var existing = all.FirstOrDefault(r => r.NormalizedKey == key && (CountsForUniqueness(r) || r.Status == CompetitorProductStatuses.Merged));
        if (existing is not null)
        {
            var target = existing;
            for (var hop = 0; hop < 5 && target.Status == CompetitorProductStatuses.Merged && target.MergedIntoId is { } next; hop++)
            {
                target = all.FirstOrDefault(r => r.Id == next) ?? target;
            }

            return Response<CompetitorProposalResult>.Success(new CompetitorProposalResult(target.Id, target.Status, Existing: true));
        }

        var now = DateTime.UtcNow;
        var record = new CompetitorProduct
        {
            TenantId = TenantId, Name = name, Company = company, NormalizedKey = key, Status = CompetitorProductStatuses.Proposed,
            CompetesWithGlobalProductIds = [request.ProductId.Value], ProposedBy = Actor, ProposedAt = now,
            ProposedFromReportId = request.ReportId is { } rid && rid != Guid.Empty ? rid : null,
            ProposedForProductId = request.ProductId, CreatedBy = Actor
        };
        record.ChangeLog.Add(new CompetitorChange { At = now, By = Actor, Field = "proposed", NewValue = name });
        await _products.InsertAsync(record, ct);
        return Response<CompetitorProposalResult>.Success(new CompetitorProposalResult(record.Id, record.Status, Existing: false), 201);
    }

    // ═════════════════════════ proposals + the decision (manage, workflow) ═════════════════════════

    public async Task<Response<IReadOnlyList<CompetitorProposalDto>>> ProposalsAsync(CancellationToken ct)
    {
        var all = await _products.ListAsync(TenantId, ct);
        var proposals = all.Where(r => r.Status == CompetitorProductStatuses.Proposed).OrderByDescending(r => r.ProposedAt).ToList();
        var decisions = await LoadDecisionsAsync(proposals.Select(p => p.Id).ToList(), ct);
        var rows = proposals.Select(p => new CompetitorProposalDto(
            CompetitorProductDto.From(p),
            all.Where(r => r.Id != p.Id && (r.Status is CompetitorProductStatuses.Active or CompetitorProductStatuses.Inactive))
                .Select(r => Similar(r, p.Name, p)).Where(s => s.Score >= 40)
                .OrderByDescending(s => s.Score).Take(3).ToList(),
            decisions.TryGetValue(p.Id, out var d) ? ToDecisionDto(d, all) : null)).ToList();
        return Response<IReadOnlyList<CompetitorProposalDto>>.Success(rows);
    }

    public async Task<Response<CompetitorProposalDto>> ProposalAsync(Guid proposalId, CancellationToken ct)
    {
        var all = await _products.ListAsync(TenantId, ct);
        var proposal = all.FirstOrDefault(r => r.Id == proposalId);
        if (proposal is null || proposal.ProposedAt is null)
        {
            return CompetitorErrors.Fail<CompetitorProposalDto>(CompetitorErrors.NotFound, "Proposal not found.", 404);
        }

        var decisions = await LoadDecisionsAsync([proposalId], ct);
        if (decisions.TryGetValue(proposalId, out var decision))
        {
            await _flow.FillReturnCommentsAsync(decision, ct);
        }

        if (decision is not null)
        {
            // the reconcile may just have applied the decision: read the proposal again
            all = await _products.ListAsync(TenantId, ct);
            proposal = all.FirstOrDefault(r => r.Id == proposalId) ?? proposal;
        }

        return Response<CompetitorProposalDto>.Success(new CompetitorProposalDto(
            CompetitorProductDto.From(proposal, await SurvivorAsync(proposal, ct)),
            all.Where(r => r.Id != proposal.Id && (r.Status is CompetitorProductStatuses.Active or CompetitorProductStatuses.Inactive))
                .Select(r => Similar(r, proposal.Name, proposal)).Where(s => s.Score >= 40).OrderByDescending(s => s.Score).Take(3).ToList(),
            decision is null ? null : ToDecisionDto(decision, all)));
    }

    /// <summary>Prepares (or re-prepares) the decision draft of a proposal. Locked while it is in review.</summary>
    public async Task<Response<CompetitorDecisionDto>> SaveDecisionAsync(Guid proposalId, CompetitorDecisionRequest request, CancellationToken ct)
    {
        var proposal = await _products.GetAsync(TenantId, proposalId, ct);
        if (proposal is null)
        {
            return CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.NotFound, "Proposal not found.", 404);
        }

        if (proposal.Status != CompetitorProductStatuses.Proposed)
        {
            return CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.NotProposal, "The proposal has already been decided.", 409);
        }

        var action = request.Action?.Trim().ToLowerInvariant();
        if (!CompetitorDecisionActions.IsKnown(action))
        {
            return CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.Invalid, "The action is complete, merge or reject.", 400);
        }

        var decision = await _decisions.GetByProposalAsync(TenantId, proposalId, ct);
        if (decision is not null && !CompetitorDecisionStatuses.IsEditable(decision.Status))
        {
            return CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.DecisionLocked, "The decision is locked while it is in review.", 409);
        }

        if (decision is not null && request.ExpectedVersion is { } ev && ev != decision.Version)
        {
            return Conflict<CompetitorDecisionDto>();
        }

        CompetitorProductFields? complete = null;
        Guid? targetId = null;
        var move = new List<string>();
        string? reason = null;
        switch (action)
        {
            case CompetitorDecisionActions.Complete:
                complete = CompetitorValidation.Normalize((request.Complete ?? new CompetitorProductWriteRequest(proposal.Name, null, proposal.Company,
                    null, null, null, null, proposal.CompetesWithGlobalProductIds, null, null)).ToFields());
                if (await CheckAsync<CompetitorDecisionDto>(complete, proposal.Id, ct) is { } failure)
                {
                    return failure;
                }

                break;
            case CompetitorDecisionActions.Merge:
                var target = request.TargetId is { } t ? await _products.GetAsync(TenantId, t, ct) : null;
                if (request.TargetId == proposalId)
                {
                    return CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.MergeSelf, "A proposal cannot be merged into itself.", 400);
                }

                if (target is null || !target.IsSelectable)
                {
                    return CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.NotSelectable, "The merge target must be an active record.", 409);
                }

                targetId = target.Id;
                move = (request.MoveMarketCodes ?? []).Select(c => c.Trim().ToUpperInvariant())
                    .Where(c => proposal.CountryPrices.Any(r => r.MarketCode == c)).Distinct().ToList();
                break;
            default:
                reason = CompetitorValidation.Clean(request.Reason);
                if (reason is null || reason.Length > CompetitorLimits.Reason)
                {
                    return CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.Invalid, $"A rejection needs a reason (≤ {CompetitorLimits.Reason}).", 400);
                }

                break;
        }

        var now = DateTimeOffset.UtcNow;
        if (decision is null)
        {
            decision = new CompetitorProposalDecision
            {
                TenantId = TenantId, ProposalId = proposalId, Action = action!, Complete = complete, TargetId = targetId,
                MoveMarketCodes = move, Reason = reason, Status = CompetitorDecisionStatuses.Draft, CreatedBy = Actor, UpdatedBy = Actor,
                UpdatedAt = now, Version = 0
            };
            await _decisions.InsertAsync(decision, ct);
        }
        else
        {
            var expected = decision.Version;
            decision.Action = action!;
            decision.Complete = complete;
            decision.TargetId = targetId;
            decision.MoveMarketCodes = move;
            decision.Reason = reason;
            decision.UpdatedBy = Actor;
            decision.UpdatedAt = now;
            if (!await _decisions.ReplaceAsync(decision, expected, ct))
            {
                return Conflict<CompetitorDecisionDto>();
            }
        }

        return Response<CompetitorDecisionDto>.Success(ToDecisionDto(decision, await _products.ListAsync(TenantId, ct)));
    }

    public async Task<Response<CompetitorDecisionDto>> SubmitDecisionAsync(Guid proposalId, CancellationToken ct)
    {
        var (proposal, decision, failure) = await OpenDecisionAsync(proposalId, ct);
        if (failure is not null) return failure;
        if (!CompetitorDecisionStatuses.IsEditable(decision!.Status))
        {
            return CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.DecisionLocked, "The decision is already in review.", 409);
        }

        var kind = WorkflowApprovalKind.CompetitorProposal;
        var display = new WorkflowDisplayContext(
            $"Rakip ürün önerisi · {ActionLabel(decision.Action)}", proposal!.Name, MdmApprovalDefaults.SourceModule, kind.Link(proposal.Id),
            new[] { proposal.Name, proposal.Company, ActionLabel(decision.Action) }.Where(c => !string.IsNullOrWhiteSpace(c)).ToList());
        var result = await _flow.SubmitAsync<CompetitorProposalDecision, CompetitorDecisionDto>(_decisionStore, decision, kind,
            _settings.CompetitorProposalTemplateCode, display, CompetitorDecisionStatuses.InReview, ct);
        return result ?? await DecisionResultAsync(proposalId, ct);
    }

    public async Task<Response<CompetitorDecisionDto>> WithdrawDecisionAsync(Guid proposalId, CancellationToken ct)
    {
        var (_, decision, failure) = await OpenDecisionAsync(proposalId, ct);
        if (failure is not null) return failure;
        var result = await _flow.WithdrawAsync<CompetitorProposalDecision, CompetitorDecisionDto>(decision!, WorkflowApprovalKind.CompetitorProposal, ct);
        return result ?? await DecisionResultAsync(proposalId, ct);
    }

    public async Task<Response<CompetitorDecisionDto>> ReviewDecisionAsync(Guid proposalId, CompetitorReviewDecisionRequest request, CancellationToken ct)
    {
        var (_, decision, failure) = await OpenDecisionAsync(proposalId, ct);
        if (failure is not null) return failure;
        var result = await _flow.DecideAsync<CompetitorProposalDecision, CompetitorDecisionDto>(_decisionStore, decision!,
            WorkflowApprovalKind.CompetitorProposal, request.Decision, request.Comment, CompetitorErrors.Sod, ct);
        return result ?? await DecisionResultAsync(proposalId, ct);
    }

    // ═════════════════════════ helpers ═════════════════════════

    /// <summary>Copies the editable fields onto a record (the change log names every changed field; with
    /// <paramref name="history"/> a changed / removed market row closes its period into the price history).</summary>
    public static void ApplyFields(CompetitorProduct record, CompetitorProductFields f, string? actor, DateTime now, bool history)
    {
        void Log(string field, string? oldValue, string? newValue)
        {
            if (history && !string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
            {
                record.ChangeLog.Add(new CompetitorChange { At = now, By = actor, Field = field, OldValue = oldValue, NewValue = newValue });
            }
        }

        Log("name", record.Name, f.Name);
        Log("company", record.Company, f.Company);
        Log("aliases", string.Join(", ", record.Aliases), string.Join(", ", f.Aliases));
        Log("activeIngredients", string.Join(", ", record.ActiveIngredients), string.Join(", ", f.ActiveIngredients));
        Log("formCode", record.FormCode, f.FormCode);
        Log("strength", record.Strength, f.Strength);
        Log("atcCode", record.AtcCode, f.AtcCode);
        Log("competesWith", string.Join(",", record.CompetesWithGlobalProductIds), string.Join(",", f.CompetesWithGlobalProductIds));
        Log("indications", string.Join(",", record.IndicationNodeIds), string.Join(",", f.IndicationNodeIds));

        if (history)
        {
            foreach (var old in record.CountryPrices)
            {
                var next = f.CountryPrices.FirstOrDefault(r => r.MarketCode == old.MarketCode);
                if (next is null)
                {
                    record.CountryPriceHistory.Add(Closed(old, now.Date, actor, now, "removed"));
                    Log($"countryPrices.{old.MarketCode}", Describe(old), null);
                }
                else if (old.BoxPrice != next.BoxPrice || old.CurrencyCode != next.CurrencyCode
                         || old.ReimbursementStatusCode != next.ReimbursementStatusCode || old.ValidFrom != next.ValidFrom
                         || old.Source != next.Source)
                {
                    // A price change is a NEW PERIOD of the same row: the old one closes the day the new one starts.
                    var validTo = next.ValidFrom > old.ValidFrom ? next.ValidFrom : now.Date;
                    record.CountryPriceHistory.Add(Closed(old, validTo, actor, now, "changed"));
                    Log($"countryPrices.{old.MarketCode}", Describe(old), Describe(next));
                }
            }

            foreach (var added in f.CountryPrices.Where(r => record.CountryPrices.All(o => o.MarketCode != r.MarketCode)))
            {
                Log($"countryPrices.{added.MarketCode}", null, Describe(added));
            }
        }

        record.Name = f.Name;
        record.Company = f.Company;
        record.NormalizedKey = CompetitorNames.Key(f.Company, f.Name);
        record.Aliases = f.Aliases.ToList();
        record.ActiveIngredients = f.ActiveIngredients.ToList();
        record.FormCode = f.FormCode;
        record.Strength = f.Strength;
        record.AtcCode = f.AtcCode;
        record.CompetesWithGlobalProductIds = f.CompetesWithGlobalProductIds.ToList();
        record.IndicationNodeIds = f.IndicationNodeIds.ToList();
        record.CountryPrices = f.CountryPrices.Select(r => new CompetitorCountryPrice
        {
            MarketCode = r.MarketCode, BoxPrice = r.BoxPrice, CurrencyCode = r.CurrencyCode,
            ReimbursementStatusCode = r.ReimbursementStatusCode, ValidFrom = r.ValidFrom, Source = r.Source
        }).ToList();
        record.UpdatedBy = actor;
    }

    private static CompetitorPriceHistoryEntry Closed(CompetitorCountryPrice row, DateTime validTo, string? actor, DateTime now, string reason) => new()
    {
        MarketCode = row.MarketCode, BoxPrice = row.BoxPrice, CurrencyCode = row.CurrencyCode,
        ReimbursementStatusCode = row.ReimbursementStatusCode, ValidFrom = row.ValidFrom,
        ValidTo = DateTime.SpecifyKind(validTo.Date, DateTimeKind.Utc), Source = row.Source, ChangedBy = actor, ChangedAt = now, Reason = reason
    };

    private static string Describe(CompetitorCountryPrice r)
        => $"{r.BoxPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)} {r.CurrencyCode} · {r.ReimbursementStatusCode} · {r.ValidFrom:yyyy-MM-dd}";

    /// <summary>Shape + uniqueness + references (markets: the verified market catalog; reimbursement: the tenant BRD set).</summary>
    private async Task<Response<T>?> CheckAsync<T>(CompetitorProductFields fields, Guid? selfId, CancellationToken ct)
    {
        if (CompetitorValidation.Check(fields) is { } shape)
        {
            return CompetitorErrors.Fail<T>(shape.Code, shape.Message, 400);
        }

        var key = CompetitorNames.Key(fields.Company, fields.Name);
        var all = await _products.ListAsync(TenantId, ct);
        if (all.Any(r => r.Id != selfId && r.NormalizedKey == key && CountsForUniqueness(r)))
        {
            return CompetitorErrors.Fail<T>(CompetitorErrors.Duplicate, "A competitor product with this company and name already exists.", 409);
        }

        if (fields.CountryPrices.Count == 0)
        {
            return null;
        }

        var reimbursement = await _referenceSets.ActiveCodesAsync(CompetitorReferenceSets.ReimbursementStatus, ct);
        if (reimbursement is null)
        {
            return CompetitorErrors.Fail<T>(CompetitorErrors.ReferenceDataUnavailable,
                $"The reference set '{CompetitorReferenceSets.ReimbursementStatus}' could not be read. Nothing was saved.", 503);
        }

        foreach (var row in fields.CountryPrices)
        {
            if (!reimbursement.Contains(row.ReimbursementStatusCode))
            {
                return CompetitorErrors.Fail<T>(CompetitorErrors.ReimbursementInvalid,
                    $"'{row.ReimbursementStatusCode}' is not an active '{CompetitorReferenceSets.ReimbursementStatus}' value.", 400);
            }

            var market = await _markets.ResolveLatestAsync(row.MarketCode, ct);
            if (!market.IsSuccessful)
            {
                return market.StatusCode is 0 or >= 500
                    ? CompetitorErrors.Fail<T>(CompetitorErrors.ReferenceDataUnavailable, "The market catalog could not be read. Nothing was saved.", 503)
                    : CompetitorErrors.Fail<T>(CompetitorErrors.MarketInvalid, $"'{row.MarketCode}' is not a market of the verified catalog.", 400);
            }
        }

        return null;
    }

    private static bool IsMyProposal(CompetitorProduct r, string? actor)
        => r.Status == CompetitorProductStatuses.Proposed && WorkflowApprovalRules.SamePerson(actor, r.ProposedBy);

    private static CompetitorSimilarDto Similar(CompetitorProduct candidate, string? probe, CompetitorProduct? source)
    {
        var score = new[] { probe }.Concat(source?.Aliases ?? []).Select(n => CompetitorNames.BestSimilarity(n, candidate)).DefaultIfEmpty(0).Max();
        return new CompetitorSimilarDto(candidate.Id, candidate.Name, candidate.Company, candidate.Status, score, score >= CompetitorLimits.LikelySameScore);
    }

    private async Task<CompetitorSummaryDto?> SurvivorAsync(CompetitorProduct record, CancellationToken ct)
    {
        var current = record;
        for (var hop = 0; hop < 5 && current.Status == CompetitorProductStatuses.Merged && current.MergedIntoId is { } next; hop++)
        {
            var survivor = await _products.GetAsync(TenantId, next, ct);
            if (survivor is null) break;
            current = survivor;
        }

        return ReferenceEquals(current, record) ? null : new CompetitorSummaryDto(current.Id, current.Name, current.Company, current.Status);
    }

    private async Task<Dictionary<Guid, CompetitorProposalDecision>> LoadDecisionsAsync(IReadOnlyCollection<Guid> proposalIds, CancellationToken ct)
    {
        if (proposalIds.Count == 0) return new();
        var decisions = await _decisions.ListByProposalsAsync(TenantId, proposalIds, ct);
        // Reconcile-on-read: an open round older than the window is checked against MOD-0023 and applied.
        if (await _reconciler.ReconcileAsync(TenantId, WorkflowApprovalKind.CompetitorProposal, decisions, ct) > 0)
        {
            decisions = await _decisions.ListByProposalsAsync(TenantId, proposalIds, ct);
        }

        return decisions.GroupBy(d => d.ProposalId).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.CreatedAt).First());
    }

    private async Task<(CompetitorProduct? Proposal, CompetitorProposalDecision? Decision, Response<CompetitorDecisionDto>? Failure)> OpenDecisionAsync(
        Guid proposalId, CancellationToken ct)
    {
        var proposal = await _products.GetAsync(TenantId, proposalId, ct);
        if (proposal is null)
        {
            return (null, null, CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.NotFound, "Proposal not found.", 404));
        }

        var decision = await _decisions.GetByProposalAsync(TenantId, proposalId, ct);
        return decision is null
            ? (proposal, null, CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.DecisionMissing, "Prepare the decision first.", 409))
            : (proposal, decision, null);
    }

    private async Task<Response<CompetitorDecisionDto>> DecisionResultAsync(Guid proposalId, CancellationToken ct)
    {
        var decision = await _decisions.GetByProposalAsync(TenantId, proposalId, ct);
        return decision is null
            ? CompetitorErrors.Fail<CompetitorDecisionDto>(CompetitorErrors.DecisionMissing, "The decision was not found.", 404)
            : Response<CompetitorDecisionDto>.Success(ToDecisionDto(decision, await _products.ListAsync(TenantId, ct)));
    }

    private CompetitorDecisionDto ToDecisionDto(CompetitorProposalDecision d, IReadOnlyList<CompetitorProduct> all)
    {
        var actor = Actor;
        var open = d.OpenRound();
        var editable = CompetitorDecisionStatuses.IsEditable(d.Status);
        var target = d.TargetId is { } t ? all.FirstOrDefault(r => r.Id == t) : null;
        var abilities = new CompetitorDecisionAbilities(
            CanEdit: CanManage && editable,
            CanSubmit: CanManage && editable,
            CanWithdraw: open is not null && WorkflowApprovalRules.SamePerson(actor, open.SubmittedBy),
            CanDecide: CanManage && open is not null && !WorkflowApprovalRules.IsOwn(d, actor));
        return new CompetitorDecisionDto(d.Id, d.ProposalId, d.Action, d.Complete, d.TargetId,
            target is null ? null : new CompetitorSummaryDto(target.Id, target.Name, target.Company, target.Status),
            d.MoveMarketCodes, d.Reason, d.Status, d.CreatedBy, d.UpdatedBy, d.UpdatedAt, d.AppliedBy, d.AppliedAt,
            open?.SubmittedBy, open?.SubmittedAt, ApprovalRoundDto.From(d), ApprovalDecisionDto.From(d), d.Version, abilities);
    }

    private static string ActionLabel(string action) => action switch
    {
        CompetitorDecisionActions.Complete => "Tamamla",
        CompetitorDecisionActions.Merge => "Birleştir",
        _ => "Reddet"
    };

    private static Response<T> Conflict<T>()
        => CompetitorErrors.Fail<T>(CompetitorErrors.ConcurrencyConflict, "The record was modified by another writer; reload and retry.", 409);
}

public sealed record CompetitorProposalResult(Guid Id, string Status, bool Existing);
