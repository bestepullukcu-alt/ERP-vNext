using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Commands;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Validators;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Handlers.CommandHandlers;

/// <summary>What the price list command handlers share: the tenant, the actor, the read of a list with its phase.</summary>
public sealed class PriceListCommandContext
{
    public PriceListCommandContext(ITenantContext tenant, IActorContext actor, IPriceListRepository lists, IPriceListItemRepository items,
        IPricingLegalEntityDirectory legalEntities, PriceListItemWriter itemWriter, PricingChangeLogWriter log, TimeProvider? clock = null)
    {
        Tenant = tenant;
        Actor = actor;
        Lists = lists;
        Items = items;
        LegalEntities = legalEntities;
        ItemWriter = itemWriter;
        Log = log;
        Clock = clock ?? TimeProvider.System;
    }

    public ITenantContext Tenant { get; }
    public IActorContext Actor { get; }
    public IPriceListRepository Lists { get; }
    public IPriceListItemRepository Items { get; }
    public IPricingLegalEntityDirectory LegalEntities { get; }
    public PriceListItemWriter ItemWriter { get; }
    public PricingChangeLogWriter Log { get; }
    public TimeProvider Clock { get; }

    public async Task<PriceList?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        => await Lists.GetByIdAsync(tenantId, id, ct) is { } list && list.TenantId == tenantId && !list.IsDeleted ? list : null;

    public async Task<Response<PriceListDto>> DtoAsync(PriceList list, CancellationToken ct)
    {
        var sameKey = await Lists.ListPublishedAsync(list.TenantId, list.LegalEntityId, list.CountryCode, ct);
        return Response<PriceListDto>.Success(PriceListMapping.ToDto(list, sameKey, PricingCommon.Today(Clock)));
    }

    public static Response<PriceListDto> Created(Response<PriceListDto> dto) => dto.IsSuccessful ? Response<PriceListDto>.Success(dto.Data!, 201) : dto;

    /// <summary>Refreshes the item count of a draft after an item change (one more optimistic write on the list).</summary>
    public async Task<bool> RecountAsync(PriceList list, CancellationToken ct)
    {
        list.ItemCount = await Items.CountAsync(list.TenantId, list.Id, ct);
        return await Lists.ReplaceAsync(list, list.Version, ct);
    }
}

public sealed class CreatePriceListHandler : IRequestHandler<CreatePriceListCommand, Response<PriceListDto>>
{
    private readonly PriceListCommandContext _c;

    public CreatePriceListHandler(PriceListCommandContext context) => _c = context;

    public async Task<Response<PriceListDto>> Handle(CreatePriceListCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListDto>("Tenant context is required.");
        }

        var input = request.Input;
        if (PriceListValidator.Check(input, create: true) is { } problem)
        {
            return PricingCommon.Fail<PriceListDto>(problem.Code, problem.Message, 400);
        }

        if (PriceListValidator.CheckPeriod(input.ValidFrom!.Value, input.ValidTo) is { } period)
        {
            return PricingCommon.Fail<PriceListDto>(period.Code, period.Message, 400);
        }

        var lookup = await _c.LegalEntities.GetAsync(input.LegalEntityId!.Value, ct);
        if (lookup.Outcome == PricingLookupOutcome.Unavailable)
        {
            return PricingCommon.MdmUnavailable<PriceListDto>();
        }

        if (lookup.LegalEntity is not { } le)
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.LegalEntityInvalid, "The legal entity does not exist in this tenant.", 400);
        }

        var actor = _c.Actor.ActorName;
        var list = new PriceList
        {
            TenantId = tenantId, Name = PricingCommon.Clean(input.Name)!, LegalEntityId = le.Id, LegalEntityName = le.Name, LegalEntityCode = le.Code,
            CountryCode = le.CountryCode.Trim().ToUpperInvariant(), Currency = PricingCommon.NormalizeCurrency(input.Currency),
            Kind = PricingCommon.Clean(input.Kind)!.ToLowerInvariant(), ValidFrom = input.ValidFrom.Value, ValidTo = input.ValidTo,
            Status = PriceListStatuses.Draft, ListVersion = 1, CreatedBy = actor, UpdatedBy = actor
        };
        list.FamilyId = list.Id;
        await _c.Lists.InsertAsync(list, ct);
        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PriceList, list.Id, list.Version, list.FamilyId, PricingChangeActions.Created, actor,
            PricingChangeLogWriter.Created(("name", list.Name), ("legalEntity", list.LegalEntityCode), ("country", list.CountryCode),
                ("currency", list.Currency), ("kind", list.Kind), ("validFrom", list.ValidFrom), ("validTo", list.ValidTo)), ct);

        if (input.Items is { Count: > 0 } items && await _c.ItemWriter.ApplyAsync(list, items, null, actor, ct) is { } refused)
        {
            // the header stands (a draft); the items are reported — the caller fixes and saves them on the draft
            return refused;
        }

        if (input.Items is { Count: > 0 })
        {
            await _c.RecountAsync(list, ct);
        }

        return PriceListCommandContext.Created(await _c.DtoAsync(list, ct));
    }
}

public sealed class UpdatePriceListHandler : IRequestHandler<UpdatePriceListCommand, Response<PriceListDto>>
{
    private readonly PriceListCommandContext _c;

    public UpdatePriceListHandler(PriceListCommandContext context) => _c = context;

    public async Task<Response<PriceListDto>> Handle(UpdatePriceListCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListDto>("Tenant context is required.");
        }

        var input = request.Input;
        if (PriceListValidator.Check(input, create: false) is { } problem)
        {
            return PricingCommon.Fail<PriceListDto>(problem.Code, problem.Message, 400);
        }

        if (input.LegalEntityId is { } le && le != Guid.Empty)
        {
            return PricingCommon.Invalid<PriceListDto>("The legal entity of a list is fixed; create a new list for another company.");
        }

        var list = await _c.GetAsync(tenantId, request.Id, ct);
        if (list is null)
        {
            return PricingCommon.NotFound<PriceListDto>();
        }

        if (!list.IsDraft())
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListNotEditable,
                "Only a draft list is edited; a published list changes through a new version.", 409);
        }

        if (input.ExpectedVersion is { } expected && expected != list.Version)
        {
            return PricingCommon.Stale<PriceListDto>();
        }

        var validFrom = input.ValidFrom ?? list.ValidFrom;
        var validTo = input.ValidFrom is null && input.ValidTo is null ? list.ValidTo : input.ValidTo;
        if (PriceListValidator.CheckPeriod(validFrom, validTo) is { } period)
        {
            return PricingCommon.Fail<PriceListDto>(period.Code, period.Message, 400);
        }

        var actor = _c.Actor.ActorName;
        var name = PricingCommon.Clean(input.Name) ?? list.Name;
        var currency = input.Currency is null ? list.Currency : PricingCommon.NormalizeCurrency(input.Currency);
        var kind = PricingCommon.Clean(input.Kind)?.ToLowerInvariant() ?? list.Kind;
        var changes = PricingChangeLogWriter.Diff(("name", list.Name, name), ("currency", list.Currency, currency), ("kind", list.Kind, kind),
            ("validFrom", list.ValidFrom, validFrom), ("validTo", list.ValidTo, validTo));
        if (changes.Count > 0)
        {
            list.Name = name;
            list.Currency = currency;
            list.Kind = kind;
            list.ValidFrom = validFrom;
            list.ValidTo = validTo;
            list.UpdatedBy = actor;
            list.UpdatedAt = DateTimeOffset.UtcNow;
            if (!await _c.Lists.ReplaceAsync(list, list.Version, ct))
            {
                return PricingCommon.Stale<PriceListDto>();
            }

            await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PriceList, list.Id, list.Version, list.FamilyId, PricingChangeActions.Updated,
                actor, changes, ct);
        }

        if (await _c.ItemWriter.ApplyAsync(list, input.Items, input.RemoveItemIds, actor, ct) is { } refused)
        {
            return refused;
        }

        if (input.Items is { Count: > 0 } || input.RemoveItemIds is { Count: > 0 })
        {
            list.UpdatedBy = actor;
            list.UpdatedAt = DateTimeOffset.UtcNow;
            if (!await _c.RecountAsync(list, ct))
            {
                return PricingCommon.Stale<PriceListDto>();
            }
        }

        return await _c.DtoAsync(list, ct);
    }
}

/// <summary>A published (or any non-discarded) list copied into a new DRAFT version of the same family (§12); the source
/// never changes.</summary>
public sealed class CreatePriceListVersionHandler : IRequestHandler<CreatePriceListVersionCommand, Response<PriceListDto>>
{
    private readonly PriceListCommandContext _c;

    public CreatePriceListVersionHandler(PriceListCommandContext context) => _c = context;

    public async Task<Response<PriceListDto>> Handle(CreatePriceListVersionCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListDto>("Tenant context is required.");
        }

        var source = await _c.GetAsync(tenantId, request.Id, ct);
        if (source is null)
        {
            return PricingCommon.NotFound<PriceListDto>();
        }

        if (!source.IsPublished())
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListNotPublished, "A new version is made from a published list.", 409);
        }

        var validFrom = request.Input?.ValidFrom ?? PricingCommon.Today(_c.Clock);
        if (PriceListValidator.CheckPeriod(validFrom, request.Input?.ValidTo) is { } period)
        {
            return PricingCommon.Fail<PriceListDto>(period.Code, period.Message, 400);
        }

        var name = PricingCommon.Clean(request.Input?.Name) ?? source.Name;
        if (name.Length > PricingLimits.NameMax)
        {
            return PricingCommon.Invalid<PriceListDto>($"The name is at most {PricingLimits.NameMax} characters.");
        }

        var actor = _c.Actor.ActorName;
        var family = await _c.Lists.ListAsync(tenantId, new PriceListFilter(source.LegalEntityId, source.CountryCode), 0, int.MaxValue, ct);
        var draft = new PriceList
        {
            TenantId = tenantId, Name = name, LegalEntityId = source.LegalEntityId, LegalEntityName = source.LegalEntityName,
            LegalEntityCode = source.LegalEntityCode, CountryCode = source.CountryCode, Currency = source.Currency, Kind = source.Kind,
            ValidFrom = validFrom, ValidTo = request.Input?.ValidTo, Status = PriceListStatuses.Draft, FamilyId = source.FamilyId,
            ListVersion = family.Items.Where(l => l.FamilyId == source.FamilyId).Select(l => l.ListVersion).DefaultIfEmpty(source.ListVersion).Max() + 1,
            PreviousPriceListId = source.Id, CreatedBy = actor, UpdatedBy = actor
        };

        var items = (await _c.Items.ListAllAsync(tenantId, source.Id, ct)).Where(i => !i.IsDeleted && i.TenantId == tenantId)
            .Select(i => new PriceListItem
            {
                TenantId = tenantId, PriceListId = draft.Id, SkuId = i.SkuId, SkuLevel = i.SkuLevel, SkuCode = i.SkuCode, SkuName = i.SkuName,
                Gtin = i.Gtin, DepotPrice = i.DepotPrice, PharmacistPurchasePrice = i.PharmacistPurchasePrice, PublicPrice = i.PublicPrice,
                VatRate = i.VatRate, CreatedBy = actor
            }).ToList();
        draft.ItemCount = items.Count;
        await _c.Lists.InsertAsync(draft, ct);
        if (items.Count > 0)
        {
            await _c.Items.InsertManyAsync(items, ct);
        }

        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PriceList, draft.Id, draft.Version, draft.FamilyId,
            PricingChangeActions.VersionCreated, actor,
            PricingChangeLogWriter.Created(("previousPriceListId", source.Id.ToString("D")), ("listVersion", draft.ListVersion),
                ("validFrom", draft.ValidFrom), ("items", items.Count)), ct);
        return PriceListCommandContext.Created(await _c.DtoAsync(draft, ct));
    }
}

public sealed class DiscardPriceListHandler : IRequestHandler<DiscardPriceListCommand, Response<PriceListDto>>
{
    private readonly PriceListCommandContext _c;

    public DiscardPriceListHandler(PriceListCommandContext context) => _c = context;

    public async Task<Response<PriceListDto>> Handle(DiscardPriceListCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListDto>("Tenant context is required.");
        }

        var list = await _c.GetAsync(tenantId, request.Id, ct);
        if (list is null)
        {
            return PricingCommon.NotFound<PriceListDto>();
        }

        if (!list.IsDraft())
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListNotEditable, "Only a draft list is discarded.", 409);
        }

        if (request.ExpectedVersion is { } expected && expected != list.Version)
        {
            return PricingCommon.Stale<PriceListDto>();
        }

        list.Status = PriceListStatuses.Discarded;
        list.UpdatedBy = _c.Actor.ActorName;
        list.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await _c.Lists.ReplaceAsync(list, list.Version, ct))
        {
            return PricingCommon.Stale<PriceListDto>();
        }

        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PriceList, list.Id, list.Version, list.FamilyId, PricingChangeActions.Discarded,
            list.UpdatedBy, PricingChangeLogWriter.Diff(("status", PriceListStatuses.Draft, PriceListStatuses.Discarded)), ct);
        return await _c.DtoAsync(list, ct);
    }
}

/// <summary>Sends a draft for publication (D13): at least one item, no published list of the same (legal entity, country)
/// beginning the same day; a MOD-0023 round starts on the publication template (503 when the workflow cannot be reached,
/// 409 when the template does not exist — nothing changed).</summary>
public sealed class SubmitPriceListHandler : IRequestHandler<SubmitPriceListCommand, Response<PriceListDto>>
{
    private readonly PriceListCommandContext _c;
    private readonly WorkflowApprovalFlow _flow;
    private readonly PricingApprovalOutcomeApplier _applier;
    private readonly IPricingApprovalSettings? _settings;

    public SubmitPriceListHandler(PriceListCommandContext context, WorkflowApprovalFlow flow, PricingApprovalOutcomeApplier applier,
        IPricingApprovalSettings? settings = null)
    {
        _c = context;
        _flow = flow;
        _applier = applier;
        _settings = settings;
    }

    public async Task<Response<PriceListDto>> Handle(SubmitPriceListCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListDto>("Tenant context is required.");
        }

        var list = await _c.GetAsync(tenantId, request.Id, ct);
        if (list is null)
        {
            return PricingCommon.NotFound<PriceListDto>();
        }

        if (!list.IsDraft())
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListNotEditable, "Only a draft list is sent for publication.", 409);
        }

        if (request.ExpectedVersion is { } expected && expected != list.Version)
        {
            return PricingCommon.Stale<PriceListDto>();
        }

        if (await _c.Items.CountAsync(tenantId, list.Id, ct) == 0)
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListEmpty, "A list without items cannot be published.", 409);
        }

        if (await _applier.ListOverlapAsync(list, ct))
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListOverlap,
                "A published list of the same company and country already begins on this day.", 409);
        }

        var display = new ClaimWorkflowDisplayContext(list.Name, $"{list.LegalEntityCode ?? list.LegalEntityName} · {list.CountryCode} · {list.Currency}",
            PricingAudit.SourceModule, PricingApprovalKinds.PriceList.Link(list.Id),
            new[] { list.Kind, list.ValidFrom.ToString("yyyy-MM-dd"), "v" + list.ListVersion });
        if (await _flow.SubmitAsync<PriceList, PriceListDto>(_applier.Lists, list, PricingApprovalKinds.PriceList,
                _settings?.TemplateCode ?? PricingApprovalDefaults.TemplateCode, display, PriceListStatuses.InReview, ct) is { } refused)
        {
            return refused;
        }

        var current = await _c.GetAsync(tenantId, list.Id, ct) ?? list;
        await _c.Log.WriteAsync(tenantId, PricingChangeEntityTypes.PriceList, current.Id, current.Version, current.FamilyId,
            PricingChangeActions.Submitted, _c.Actor.ActorName, PricingChangeLogWriter.Diff(("status", PriceListStatuses.Draft, current.Status)), ct);
        return await _c.DtoAsync(current, ct);
    }
}

/// <summary>
/// The second person's decision (D13): <c>publish</c> | <c>return</c> (a return needs its reason). The submitter / author /
/// last editor never decides (403 <c>price_list_sod</c>); the first decision wins (a second finds no open round → 409). The
/// decision is taken on the SAME MOD-0023 task the Work Center shows, and applied here at once (the event bus may be off).
/// </summary>
public sealed class DecidePriceListHandler : IRequestHandler<DecidePriceListCommand, Response<PriceListDto>>
{
    private readonly PriceListCommandContext _c;
    private readonly WorkflowApprovalFlow _flow;
    private readonly PricingApprovalOutcomeApplier _applier;

    public DecidePriceListHandler(PriceListCommandContext context, WorkflowApprovalFlow flow, PricingApprovalOutcomeApplier applier)
    {
        _c = context;
        _flow = flow;
        _applier = applier;
    }

    public async Task<Response<PriceListDto>> Handle(DecidePriceListCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<PriceListDto>("Tenant context is required.");
        }

        var decision = PricingDecisions.Normalize(request.Input?.Decision);
        if (decision is null)
        {
            return PricingCommon.Invalid<PriceListDto>("The decision is publish or return.");
        }

        var reason = PricingCommon.Clean(request.Input?.Reason);
        if (decision == WorkflowApprovalDecisions.Return && reason is null)
        {
            return PricingCommon.Invalid<PriceListDto>("Returning a list to draft needs its reason.");
        }

        if (reason is { Length: > PricingLimits.ReasonMax })
        {
            return PricingCommon.Invalid<PriceListDto>($"The reason is at most {PricingLimits.ReasonMax} characters.");
        }

        var list = await _c.GetAsync(tenantId, request.Id, ct);
        if (list is null)
        {
            return PricingCommon.NotFound<PriceListDto>();
        }

        if (request.Input?.ExpectedVersion is { } expected && expected != list.Version)
        {
            return PricingCommon.Stale<PriceListDto>();
        }

        if (list.Status != PriceListStatuses.InReview || list.OpenRound() is not { } round)
        {
            return PricingCommon.Fail<PriceListDto>(ClaimErrorCodes.NoOpenReview, "This list is not waiting for a decision.", 409);
        }

        var actor = _c.Actor.ActorName;
        if (WorkflowApprovalRules.IsOwn(list, actor))
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListSod,
                "The author, the last editor or the submitter cannot decide on the list (separation of duties).", 403);
        }

        if (decision == WorkflowApprovalDecisions.Approve && await _applier.ListOverlapAsync(list, ct))
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListOverlap,
                "A published list of the same company and country already begins on this day.", 409);
        }

        if (await _flow.DecideAsync<PriceList, PriceListDto>(_applier.Lists, list, PricingApprovalKinds.PriceList, decision, reason,
                PricingErrors.PriceListSod, ct) is { } refused)
        {
            return refused;
        }

        await _applier.ApplyAsync(tenantId, PricingApprovalKinds.PriceList.ObjectType, list.Id, round.WorkflowInstanceId,
            decision == WorkflowApprovalDecisions.Approve ? ClaimReviewOutcomes.Approved : ClaimReviewOutcomes.Rejected,
            actor, reason, _c.Clock.GetUtcNow(), ct);
        return await _c.GetAsync(tenantId, list.Id, ct) is { } current ? await _c.DtoAsync(current, ct) : PricingCommon.NotFound<PriceListDto>();
    }
}

/// <summary>The wire decisions of MOD-0156 (<c>publish</c> | <c>return</c>) onto the workflow's (<c>approve</c> | <c>return</c>).</summary>
public static class PricingDecisions
{
    public const string Publish = "publish";
    public const string Return = "return";

    public static string? Normalize(string? decision) => decision?.Trim().ToLowerInvariant() switch
    {
        Publish or WorkflowApprovalDecisions.Approve => WorkflowApprovalDecisions.Approve,
        Return => WorkflowApprovalDecisions.Return,
        _ => null
    };
}
