using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Commands;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Queries;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Validators;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Handlers;

/// <summary>What the rule handlers share.</summary>
public sealed class PricingRuleContext
{
    public PricingRuleContext(ITenantContext tenant, IActorContext actor, IPricingRuleRepository rules, IPricingLegalEntityDirectory legalEntities,
        IPricingSkuDirectory skus, PricingChangeLogWriter log)
    {
        Tenant = tenant;
        Actor = actor;
        Rules = rules;
        LegalEntities = legalEntities;
        Skus = skus;
        Log = log;
    }

    public ITenantContext Tenant { get; }
    public IActorContext Actor { get; }
    public IPricingRuleRepository Rules { get; }
    public IPricingLegalEntityDirectory LegalEntities { get; }
    public IPricingSkuDirectory Skus { get; }
    public PricingChangeLogWriter Log { get; }

    public async Task<PricingRule?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        => await Rules.GetByIdAsync(tenantId, id, ct) is { } rule && rule.TenantId == tenantId && !rule.IsDeleted ? rule : null;

    /// <summary>An open (draft / in review) version of the key, other than <paramref name="exceptId"/>.</summary>
    public async Task<bool> HasOpenVersionAsync(Guid tenantId, Guid ruleKey, Guid? exceptId, CancellationToken ct)
        => (await Rules.ListByRuleKeyAsync(tenantId, ruleKey, ct))
            .Any(r => r.Id != exceptId && r.TenantId == tenantId && r.Status is PricingRuleStatuses.Draft or PricingRuleStatuses.InReview);

    public static Response<PricingRuleDto> Ok(PricingRule rule, int status = 200) => Response<PricingRuleDto>.Success(PricingRuleMapping.ToDto(rule), status);
}

public sealed class CreatePricingRuleHandler : IRequestHandler<CreatePricingRuleCommand, Response<PricingRuleDto>>
{
    private readonly PricingRuleContext _c;

    public CreatePricingRuleHandler(PricingRuleContext context) => _c = context;

    public async Task<Response<PricingRuleDto>> Handle(CreatePricingRuleCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PricingRuleDto>("Tenant context is required.");
        }

        if (PricingRuleValidator.Check(request.Input, create: true) is { } problem)
        {
            return PricingCommon.Fail<PricingRuleDto>(problem.Code, problem.Message, 400);
        }

        var input = request.Input;
        var lookup = await _c.LegalEntities.GetAsync(input.LegalEntityId!.Value, ct);
        if (lookup.Outcome == PricingLookupOutcome.Unavailable)
        {
            return PricingCommon.MdmUnavailable<PricingRuleDto>();
        }

        if (lookup.LegalEntity is not { } le)
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.LegalEntityInvalid, "The legal entity does not exist in this tenant.", 400);
        }

        var skus = await _c.Skus.ReadAsync(new[] { input.SkuId!.Value }, ct);
        if (skus is null)
        {
            return PricingCommon.MdmUnavailable<PricingRuleDto>();
        }

        if (!skus.TryGetValue(input.SkuId.Value, out var sku))
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.SkuInvalid, "The SKU does not exist.", 400);
        }

        if (sku.IsRetired)
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.SkuRetired, $"The SKU {sku.Code} is Retired; a rule cannot be written for it.", 400);
        }

        var actor = _c.Actor.ActorName;
        var rule = new PricingRule
        {
            TenantId = tenantId, RuleKey = Guid.NewGuid(), RuleVersion = 1, LegalEntityId = le.Id, LegalEntityName = le.Name, SkuId = sku.SkuId,
            SkuLevel = sku.SkuLevel, SkuCode = sku.Code, SkuName = sku.Name, Status = PricingRuleStatuses.Draft, ChangeKind = PricingRuleChangeKinds.Create,
            CreatedBy = actor, UpdatedBy = actor
        };
        PricingRuleValidator.Apply(rule, input);
        await _c.Rules.InsertAsync(rule, ct);
        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PricingRule, rule.Id, rule.Version, rule.RuleKey, PricingChangeActions.Created, actor,
            PricingChangeLogWriter.Created([("legalEntity", le.Code), ("sku", sku.Code), .. PricingRuleValidator.Fields(rule)]), ct);
        return PricingRuleContext.Ok(rule, 201);
    }
}

/// <summary>Edits a draft; or (<see cref="UpdatePricingRuleCommand.Revision"/>) opens a revision draft of an ACTIVE rule —
/// one open version per rule key at a time.</summary>
public sealed class UpdatePricingRuleHandler : IRequestHandler<UpdatePricingRuleCommand, Response<PricingRuleDto>>
{
    private readonly PricingRuleContext _c;

    public UpdatePricingRuleHandler(PricingRuleContext context) => _c = context;

    public async Task<Response<PricingRuleDto>> Handle(UpdatePricingRuleCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PricingRuleDto>("Tenant context is required.");
        }

        if (PricingRuleValidator.Check(request.Input, create: false) is { } problem)
        {
            return PricingCommon.Fail<PricingRuleDto>(problem.Code, problem.Message, 400);
        }

        var rule = await _c.GetAsync(tenantId, request.Id, ct);
        if (rule is null)
        {
            return PricingCommon.NotFound<PricingRuleDto>();
        }

        if ((request.Input.LegalEntityId is { } le && le != rule.LegalEntityId) || (request.Input.SkuId is { } sku && sku != rule.SkuId))
        {
            return PricingCommon.Invalid<PricingRuleDto>("The legal entity and the SKU of a rule are fixed; write a new rule for another pair.");
        }

        if (request.Input.ExpectedVersion is { } expected && expected != rule.Version)
        {
            return PricingCommon.Stale<PricingRuleDto>();
        }

        var actor = _c.Actor.ActorName;
        if (request.Revision)
        {
            if (!rule.IsActive())
            {
                return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleNotActive, "A revision is opened from an active rule.", 409);
            }

            if (await _c.HasOpenVersionAsync(tenantId, rule.RuleKey, null, ct))
            {
                return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleOpenDraftExists, "This rule already has an open draft; finish it first.", 409);
            }

            var versions = await _c.Rules.ListByRuleKeyAsync(tenantId, rule.RuleKey, ct);
            var revision = Copy(rule, versions.Max(v => v.RuleVersion) + 1, PricingRuleChangeKinds.Revise, actor);
            PricingRuleValidator.Apply(revision, request.Input);
            await _c.Rules.InsertAsync(revision, ct);
            await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PricingRule, revision.Id, revision.Version, revision.RuleKey,
                PricingChangeActions.VersionCreated, actor, Changes(rule, revision), ct);
            return PricingRuleContext.Ok(revision, 201);
        }

        if (!rule.IsDraft())
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleNotEditable,
                "Only a draft is edited; an active rule changes through a revision (four eyes).", 409);
        }

        if (rule.ChangeKind == PricingRuleChangeKinds.Retire)
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleNotEditable, "A retirement request carries no limits to edit.", 409);
        }

        var before = Copy(rule, rule.RuleVersion, rule.ChangeKind, rule.UpdatedBy);
        PricingRuleValidator.Apply(rule, request.Input);
        rule.UpdatedBy = actor;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await _c.Rules.ReplaceAsync(rule, rule.Version, ct))
        {
            return PricingCommon.Stale<PricingRuleDto>();
        }

        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PricingRule, rule.Id, rule.Version, rule.RuleKey, PricingChangeActions.Updated, actor,
            Changes(before, rule), ct);
        return PricingRuleContext.Ok(rule);
    }

    internal static PricingRule Copy(PricingRule source, int version, string changeKind, string? actor) => new()
    {
        TenantId = source.TenantId, RuleKey = source.RuleKey, RuleVersion = version, LegalEntityId = source.LegalEntityId,
        LegalEntityName = source.LegalEntityName, SkuId = source.SkuId, SkuLevel = source.SkuLevel, SkuCode = source.SkuCode, SkuName = source.SkuName,
        SegmentCode = source.SegmentCode, ValidFrom = source.ValidFrom, ValidTo = source.ValidTo,
        MaxDiscountPct = new PricingPctLimit { Soft = source.MaxDiscountPct.Soft, Hard = source.MaxDiscountPct.Hard },
        MaxMf = new PricingMfLimit
        {
            Soft = source.MaxMf.Soft is { } s ? new PricingMfTerm { Buy = s.Buy, Free = s.Free } : null,
            Hard = source.MaxMf.Hard is { } h ? new PricingMfTerm { Buy = h.Buy, Free = h.Free } : null
        },
        MaxCombinedPct = new PricingPctLimit { Soft = source.MaxCombinedPct.Soft, Hard = source.MaxCombinedPct.Hard },
        Quantity = new PricingQuantityLimit { Min = source.Quantity.Min, SoftMax = source.Quantity.SoftMax, HardMax = source.Quantity.HardMax },
        Status = PricingRuleStatuses.Draft, ChangeKind = changeKind, CreatedBy = actor, UpdatedBy = actor
    };

    internal static List<PricingFieldChange> Changes(PricingRule before, PricingRule after)
    {
        var b = PricingRuleValidator.Fields(before);
        var a = PricingRuleValidator.Fields(after);
        return PricingChangeLogWriter.Diff(b.Zip(a, (x, y) => (x.Item1, x.Item2, y.Item2)).ToArray());
    }
}

public sealed class RequestPricingRuleRetirementHandler : IRequestHandler<RequestPricingRuleRetirementCommand, Response<PricingRuleDto>>
{
    private readonly PricingRuleContext _c;

    public RequestPricingRuleRetirementHandler(PricingRuleContext context) => _c = context;

    public async Task<Response<PricingRuleDto>> Handle(RequestPricingRuleRetirementCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PricingRuleDto>("Tenant context is required.");
        }

        var rule = await _c.GetAsync(tenantId, request.Id, ct);
        if (rule is null)
        {
            return PricingCommon.NotFound<PricingRuleDto>();
        }

        if (!rule.IsActive())
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleNotActive, "Only an active rule is retired.", 409);
        }

        if (await _c.HasOpenVersionAsync(tenantId, rule.RuleKey, null, ct))
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleOpenDraftExists, "This rule already has an open draft; finish it first.", 409);
        }

        var actor = _c.Actor.ActorName;
        var versions = await _c.Rules.ListByRuleKeyAsync(tenantId, rule.RuleKey, ct);
        var retirement = UpdatePricingRuleHandler.Copy(rule, versions.Max(v => v.RuleVersion) + 1, PricingRuleChangeKinds.Retire, actor);
        await _c.Rules.InsertAsync(retirement, ct);
        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PricingRule, retirement.Id, retirement.Version, retirement.RuleKey,
            PricingChangeActions.RetirementRequested, actor, PricingChangeLogWriter.Diff(("changeKind", null, PricingRuleChangeKinds.Retire)), ct);
        return PricingRuleContext.Ok(retirement, 201);
    }
}

public sealed class DiscardPricingRuleDraftHandler : IRequestHandler<DiscardPricingRuleDraftCommand, Response<PricingRuleDto>>
{
    private readonly PricingRuleContext _c;

    public DiscardPricingRuleDraftHandler(PricingRuleContext context) => _c = context;

    public async Task<Response<PricingRuleDto>> Handle(DiscardPricingRuleDraftCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PricingRuleDto>("Tenant context is required.");
        }

        var rule = await _c.GetAsync(tenantId, request.Id, ct);
        if (rule is null)
        {
            return PricingCommon.NotFound<PricingRuleDto>();
        }

        if (!rule.IsDraft())
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleNotEditable, "Only a draft is discarded.", 409);
        }

        if (request.ExpectedVersion is { } expected && expected != rule.Version)
        {
            return PricingCommon.Stale<PricingRuleDto>();
        }

        rule.Status = PricingRuleStatuses.Discarded;
        rule.UpdatedBy = _c.Actor.ActorName;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await _c.Rules.ReplaceAsync(rule, rule.Version, ct))
        {
            return PricingCommon.Stale<PricingRuleDto>();
        }

        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PricingRule, rule.Id, rule.Version, rule.RuleKey, PricingChangeActions.Discarded,
            rule.UpdatedBy, PricingChangeLogWriter.Diff(("status", PricingRuleStatuses.Draft, PricingRuleStatuses.Discarded)), ct);
        return PricingRuleContext.Ok(rule);
    }
}

public sealed class SubmitPricingRuleHandler : IRequestHandler<SubmitPricingRuleCommand, Response<PricingRuleDto>>
{
    private readonly PricingRuleContext _c;
    private readonly WorkflowApprovalFlow _flow;
    private readonly PricingApprovalOutcomeApplier _applier;
    private readonly IPricingApprovalSettings? _settings;

    public SubmitPricingRuleHandler(PricingRuleContext context, WorkflowApprovalFlow flow, PricingApprovalOutcomeApplier applier,
        IPricingApprovalSettings? settings = null)
    {
        _c = context;
        _flow = flow;
        _applier = applier;
        _settings = settings;
    }

    public async Task<Response<PricingRuleDto>> Handle(SubmitPricingRuleCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PricingRuleDto>("Tenant context is required.");
        }

        var rule = await _c.GetAsync(tenantId, request.Id, ct);
        if (rule is null)
        {
            return PricingCommon.NotFound<PricingRuleDto>();
        }

        if (!rule.IsDraft())
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleNotEditable, "Only a draft is sent for approval.", 409);
        }

        if (request.ExpectedVersion is { } expected && expected != rule.Version)
        {
            return PricingCommon.Stale<PricingRuleDto>();
        }

        if (await _applier.RuleOverlapAsync(rule, ct))
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleOverlap,
                "An active rule of the same company, SKU and specificity covers an overlapping period.", 409);
        }

        var display = new ClaimWorkflowDisplayContext($"{rule.SkuCode ?? rule.SkuId.ToString("D")} · {rule.LegalEntityName}",
            $"{rule.ChangeKind} · v{rule.RuleVersion}", PricingAudit.SourceModule, PricingApprovalKinds.PricingRule.Link(rule.Id),
            new[] { rule.ChangeKind, "v" + rule.RuleVersion });
        if (await _flow.SubmitAsync<PricingRule, PricingRuleDto>(_applier.Rules, rule, PricingApprovalKinds.PricingRule,
                _settings?.TemplateCode ?? PricingApprovalDefaults.TemplateCode, display, PricingRuleStatuses.InReview, ct) is { } refused)
        {
            return refused;
        }

        var current = await _c.GetAsync(tenantId, rule.Id, ct) ?? rule;
        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PricingRule, current.Id, current.Version, current.RuleKey, PricingChangeActions.Submitted,
            _c.Actor.ActorName, PricingChangeLogWriter.Diff(("status", PricingRuleStatuses.Draft, current.Status)), ct);
        return PricingRuleContext.Ok(current);
    }
}

public sealed class DecidePricingRuleHandler : IRequestHandler<DecidePricingRuleCommand, Response<PricingRuleDto>>
{
    private readonly PricingRuleContext _c;
    private readonly WorkflowApprovalFlow _flow;
    private readonly PricingApprovalOutcomeApplier _applier;
    private readonly TimeProvider _clock;

    public DecidePricingRuleHandler(PricingRuleContext context, WorkflowApprovalFlow flow, PricingApprovalOutcomeApplier applier, TimeProvider? clock = null)
    {
        _c = context;
        _flow = flow;
        _applier = applier;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<PricingRuleDto>> Handle(DecidePricingRuleCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PricingRuleDto>("Tenant context is required.");
        }

        var decision = PricingDecisions.Normalize(request.Input?.Decision);
        if (decision is null)
        {
            return PricingCommon.Invalid<PricingRuleDto>("The decision is publish or return.");
        }

        var reason = PricingCommon.Clean(request.Input?.Reason);
        if (decision == WorkflowApprovalDecisions.Return && reason is null)
        {
            return PricingCommon.Invalid<PricingRuleDto>("Returning a rule to draft needs its reason.");
        }

        if (reason is { Length: > PricingLimits.ReasonMax })
        {
            return PricingCommon.Invalid<PricingRuleDto>($"The reason is at most {PricingLimits.ReasonMax} characters.");
        }

        var rule = await _c.GetAsync(tenantId, request.Id, ct);
        if (rule is null)
        {
            return PricingCommon.NotFound<PricingRuleDto>();
        }

        if (request.Input?.ExpectedVersion is { } expected && expected != rule.Version)
        {
            return PricingCommon.Stale<PricingRuleDto>();
        }

        if (rule.Status != PricingRuleStatuses.InReview || rule.OpenRound() is not { } round)
        {
            return PricingCommon.Fail<PricingRuleDto>(ClaimErrorCodes.NoOpenReview, "This rule is not waiting for a decision.", 409);
        }

        var actor = _c.Actor.ActorName;
        if (WorkflowApprovalRules.IsOwn(rule, actor))
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleSod,
                "The author, the last editor or the submitter cannot decide on the rule (separation of duties).", 403);
        }

        if (decision == WorkflowApprovalDecisions.Approve && await _applier.RuleOverlapAsync(rule, ct))
        {
            return PricingCommon.Fail<PricingRuleDto>(PricingErrors.RuleOverlap,
                "An active rule of the same company, SKU and specificity covers an overlapping period.", 409);
        }

        if (await _flow.DecideAsync<PricingRule, PricingRuleDto>(_applier.Rules, rule, PricingApprovalKinds.PricingRule, decision, reason,
                PricingErrors.RuleSod, ct) is { } refused)
        {
            return refused;
        }

        await _applier.ApplyAsync(tenantId, PricingApprovalKinds.PricingRule.ObjectType, rule.Id, round.WorkflowInstanceId,
            decision == WorkflowApprovalDecisions.Approve ? ClaimReviewOutcomes.Approved : ClaimReviewOutcomes.Rejected, actor, reason,
            _clock.GetUtcNow(), ct);
        return await _c.GetAsync(tenantId, rule.Id, ct) is { } current ? PricingRuleContext.Ok(current) : PricingCommon.NotFound<PricingRuleDto>();
    }
}

public sealed class PricingRuleQueryHandlers :
    IRequestHandler<GetPricingRuleListQuery, Response<PricingRulePageDto>>,
    IRequestHandler<GetPricingRuleByIdQuery, Response<PricingRuleDetailDto>>,
    IRequestHandler<EvaluatePricingTrialQuery, Response<EvaluationResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IPricingRuleRepository _rules;
    private readonly IPricingEvaluator _evaluator;
    private readonly IPricingLegalEntityDirectory _legalEntities;
    private readonly PricingApprovalReconciler? _reconciler;
    private readonly TimeProvider _clock;

    public PricingRuleQueryHandlers(ITenantContext tenant, IPricingRuleRepository rules, IPricingEvaluator evaluator,
        IPricingLegalEntityDirectory legalEntities, PricingApprovalReconciler? reconciler = null, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _rules = rules;
        _evaluator = evaluator;
        _legalEntities = legalEntities;
        _reconciler = reconciler;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<PricingRulePageDto>> Handle(GetPricingRuleListQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PricingRulePageDto>("Tenant context is required.");
        }

        var status = PricingCommon.Clean(request.Status)?.ToLowerInvariant();
        if (status is not null && !PricingCommon.In(status, PricingRuleStatuses.All))
        {
            return PricingCommon.Invalid<PricingRulePageDto>("status is draft, in_review, active, superseded, retired or discarded.");
        }

        var (page, size) = PriceLists.Handlers.QueryHandlers.PriceListQueryHandlers.Paging(request.Page, request.PageSize);
        var (items, total) = await _rules.ListAsync(tenantId, new PricingRuleFilter(request.LegalEntityId, request.SkuId,
            PricingCommon.Clean(request.SegmentCode), status is null ? null : new[] { status }, PricingCommon.Clean(request.Search)), (page - 1) * size, size, ct);
        items = await ReconcileAsync(tenantId, items, ct);
        return Response<PricingRulePageDto>.Success(new PricingRulePageDto(
            items.Where(r => r.TenantId == tenantId).Select(PricingRuleMapping.ToDto).ToList(), total, page, size));
    }

    public async Task<Response<PricingRuleDetailDto>> Handle(GetPricingRuleByIdQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PricingRuleDetailDto>("Tenant context is required.");
        }

        var rule = await _rules.GetByIdAsync(tenantId, request.Id, ct);
        if (rule is null || rule.TenantId != tenantId || rule.IsDeleted)
        {
            return PricingCommon.NotFound<PricingRuleDetailDto>();
        }

        rule = (await ReconcileAsync(tenantId, new[] { rule }, ct)).Single();
        var versions = (await _rules.ListByRuleKeyAsync(tenantId, rule.RuleKey, ct)).Where(v => v.TenantId == tenantId && !v.IsDeleted)
            .OrderByDescending(v => v.RuleVersion).Select(PricingRuleMapping.ToDto).ToList();
        return Response<PricingRuleDetailDto>.Success(new PricingRuleDetailDto(PricingRuleMapping.ToDto(rule), versions));
    }

    public async Task<Response<EvaluationResult>> Handle(EvaluatePricingTrialQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<EvaluationResult>("Tenant context is required.");
        }

        var input = request.Input;
        if (input?.LegalEntityId is not { } legalEntityId || legalEntityId == Guid.Empty)
        {
            return PricingCommon.Fail<EvaluationResult>(PricingErrors.LegalEntityInvalid, "The legal entity is required.", 400);
        }

        if (input.Lines is not { Count: > 0 } lines || lines.Count > 50
            || lines.Any(l => l.SkuId is null || l.SkuId == Guid.Empty || l.Quantity is null or < 0
                              || l.Mf is { Buy: < 1 } or { Free: < 0 } || l.DiscountPct is < 0m or > 100m))
        {
            return PricingCommon.Invalid<EvaluationResult>("1–50 lines, each with a SKU, a quantity ≥ 0, an MF a ≥ 1 / b ≥ 0 and a discount 0–100.");
        }

        var tier = PricingCommon.Clean(input.Tier)?.ToLowerInvariant() ?? PriceTiers.PharmacistPurchase;
        if (!PricingCommon.In(tier, PriceTiers.All))
        {
            return PricingCommon.Invalid<EvaluationResult>("tier is depot, pharmacist-purchase or public.");
        }

        var country = PricingCommon.Clean(input.CountryCode)?.ToUpperInvariant();
        if (country is null)
        {
            var lookup = await _legalEntities.GetAsync(legalEntityId, ct);
            if (lookup.Outcome == PricingLookupOutcome.Unavailable)
            {
                return PricingCommon.MdmUnavailable<EvaluationResult>();
            }

            if (lookup.LegalEntity is not { } le)
            {
                return PricingCommon.Fail<EvaluationResult>(PricingErrors.LegalEntityInvalid, "The legal entity does not exist in this tenant.", 400);
            }

            country = le.CountryCode.Trim().ToUpperInvariant();
        }

        var result = await _evaluator.EvaluateAsync(new EvaluationRequest(tenantId, legalEntityId, country, input.AsOf ?? PricingCommon.Today(_clock),
            tier, PricingCommon.Clean(input.PharmacySegmentCode),
            lines.Select((l, i) => new EvaluationLine(i + 1, l.SkuId!.Value, l.Quantity!.Value, l.Mf is null ? null : new MfInput(l.Mf.Buy, l.Mf.Free),
                l.DiscountPct)).ToList()), ct);
        return Response<EvaluationResult>.Success(result);
    }

    private async Task<IReadOnlyList<PricingRule>> ReconcileAsync(Guid tenantId, IReadOnlyList<PricingRule> rules, CancellationToken ct)
    {
        if (_reconciler is null || rules.All(r => r.OpenRound() is null)
            || await _reconciler.ReconcileAsync(tenantId, PricingApprovalKinds.PricingRule, rules, ct) == 0)
        {
            return rules;
        }

        var reloaded = new List<PricingRule>(rules.Count);
        foreach (var rule in rules)
        {
            reloaded.Add(await _rules.GetByIdAsync(tenantId, rule.Id, ct) ?? rule);
        }

        return reloaded;
    }
}
