using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Drills;

/// <summary>listDrillPlans (R5) — the company's plans by code.</summary>
public sealed class ListRecallDrillPlansHandler : IRequestHandler<ListRecallDrillPlansQuery, Response<IReadOnlyList<RecallDrillPlanDto>>>
{
    private readonly IRecallDrillRepository _drills;

    public ListRecallDrillPlansHandler(IRecallDrillRepository drills) => _drills = drills;

    public async Task<Response<IReadOnlyList<RecallDrillPlanDto>>> Handle(ListRecallDrillPlansQuery request, CancellationToken ct)
        => Response<IReadOnlyList<RecallDrillPlanDto>>.Success((await _drills.ListPlansAsync(ct)).Select(RecallDrillMapping.Plan).ToList());
}

/// <summary>The DrillPlanInput rules + its fingerprint (create and update share them).</summary>
internal static class RecallDrillPlanInputs
{
    public static string Fingerprint(RecallDrillPlanInput i, params (string, string?)[] extra)
        => RecallSupport.Fingerprint(
            [("name", i.Name?.Trim()), ("frequency", i.Frequency.ToString()), ("customDays", RecallDrillMapping.I(i.CustomDays)), ("scopeKind", i.ScopeKind.ToString()),
             ("nextDueOn", i.NextDueOn.ToString(RecallSupport.DateFormat, CultureInfo.InvariantCulture)), ("ownerUserId", i.OwnerUserId.ToString("D")),
             ("active", i.Active is { } a ? (a ? "true" : "false") : null), .. extra]);

    public static RecallViolation? Refusal(RecallDrillPlanInput i)
        => RecallDrillRules.Plan(i.Name, i.Frequency, i.CustomDays, i.ScopeKind, i.NextDueOn, i.OwnerUserId);

    public static void Apply(RecallDrillPlan plan, RecallDrillPlanInput i)
    {
        plan.Name = i.Name!.Trim();
        plan.Frequency = i.Frequency;
        plan.CustomDays = i.CustomDays;
        plan.ScopeKind = i.ScopeKind;
        plan.NextDueOn = i.NextDueOn;
        plan.OwnerUserId = i.OwnerUserId;
        plan.Active = i.Active ?? true;
    }

    public static Dictionary<string, string?> Detail(RecallDrillPlan plan, string fingerprint) => new(StringComparer.Ordinal)
    {
        ["code"] = plan.Code,
        ["name"] = plan.Name,
        ["frequency"] = plan.Frequency.ToString(),
        ["customDays"] = RecallDrillMapping.I(plan.CustomDays),
        ["scopeKind"] = plan.ScopeKind.ToString(),
        ["nextDueOn"] = plan.NextDueOn.ToString(RecallSupport.DateFormat, CultureInfo.InvariantCulture),
        ["ownerUserId"] = plan.OwnerUserId.ToString("D"),
        ["active"] = plan.Active ? "true" : "false",
        ["version"] = RecallDrillMapping.I(plan.Version),
        ["fingerprint"] = fingerprint
    };
}

/// <summary>
/// createDrillPlan (R5, pack §4.11) — <c>DP-NN</c> from the company counter. ONE transaction: the plan + its event <c>PlanCreated</c> + one
/// audit row <c>recall.drill-plan.saved</c>. Same key + same body → the first plan (200); another body → 400 <c>IDEMPOTENCY_KEY_REUSED</c>.
/// </summary>
public sealed class CreateRecallDrillPlanHandler : IRequestHandler<CreateRecallDrillPlanCommand, Response<RecallDrillPlanDto>>
{
    private readonly IRecallDrillRepository _drills;
    private readonly RecallSupport _support;

    public CreateRecallDrillPlanHandler(IRecallDrillRepository drills, RecallSupport support)
    {
        _drills = drills;
        _support = support;
    }

    public async Task<Response<RecallDrillPlanDto>> Handle(CreateRecallDrillPlanCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var fingerprint = RecallDrillPlanInputs.Fingerprint(input);
        if (await ReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallDrillPlanInputs.Refusal(input) is { } invalid) return RecallSupport.Fail<RecallDrillPlanDto>(invalid);

        var now = _support.Now();
        var plan = new RecallDrillPlan
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            CreateIdempotencyKey = request.IdempotencyKey,
            CreateRequestFingerprint = fingerprint,
            CreatedByUser = _support.UserId
        };
        RecallDrillPlanInputs.Apply(plan, input);

        try
        {
            await _drills.InTransactionAsync(async (session, c) =>
            {
                plan.Code = await session.NextPlanCodeAsync(c);
                await session.InsertPlanAsync(plan, c);
                await session.InsertEventAsync(RecallDrillMapping.Event(_support, plan.Id, RecallDrillEventKind.PlanCreated, now, request.IdempotencyKey, fingerprint,
                    new Dictionary<string, string?> { ["code"] = plan.Code }), c);
                await session.InsertAuditAsync(_support.Audit(RecallDrillContract.AuditPlanSaved, RecallDrillContract.SubjectPlan, plan.Id, plan.Code, now,
                    request.IdempotencyKey, RecallDrillPlanInputs.Detail(plan, fingerprint)), c);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await ReplayAsync(request.IdempotencyKey, fingerprint, ct) ?? RecallSupport.KeyReused<RecallDrillPlanDto>();
        }

        return Response<RecallDrillPlanDto>.Success(RecallDrillMapping.Plan(plan), 201);
    }

    private async Task<Response<RecallDrillPlanDto>?> ReplayAsync(string key, string fingerprint, CancellationToken ct)
        => await _drills.FindPlanByCreateKeyAsync(_support.UserId, key, ct) is { } earlier
            ? earlier.CreateRequestFingerprint == fingerprint
                ? Response<RecallDrillPlanDto>.Success(RecallDrillMapping.Plan(earlier))
                : RecallSupport.KeyReused<RecallDrillPlanDto>()
            : null;
}

/// <summary>
/// updateDrillPlan (R5) — full replacement of the plan's fields (the code never changes), If-Match. ONE transaction: the plan + event
/// <c>PlanUpdated</c> + audit <c>recall.drill-plan.saved</c>. A started run keeps the plan code it started with.
/// </summary>
public sealed class UpdateRecallDrillPlanHandler : IRequestHandler<UpdateRecallDrillPlanCommand, Response<RecallDrillPlanDto>>
{
    private readonly IRecallDrillRepository _drills;
    private readonly RecallSupport _support;

    public UpdateRecallDrillPlanHandler(IRecallDrillRepository drills, RecallSupport support)
    {
        _drills = drills;
        _support = support;
    }

    public async Task<Response<RecallDrillPlanDto>> Handle(UpdateRecallDrillPlanCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var fingerprint = RecallDrillPlanInputs.Fingerprint(input, ("version", RecallDrillMapping.I(request.ExpectedVersion)));
        var plan = await _drills.FindPlanAsync(request.PlanId, ct);
        if (plan is null) return RecallDrillMapping.PlanNotFound<RecallDrillPlanDto>();
        if (await ReplayAsync(plan.Id, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallDrillPlanInputs.Refusal(input) is { } invalid) return RecallSupport.Fail<RecallDrillPlanDto>(invalid);
        if (plan.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallDrillPlanDto>();

        var now = _support.Now();
        RecallDrillPlanInputs.Apply(plan, input);
        plan.Version = request.ExpectedVersion + 1;
        plan.UpdatedAt = now;

        bool saved;
        try
        {
            saved = await _drills.InTransactionAsync(async (session, c) =>
            {
                if (!await session.ReplacePlanAsync(plan, request.ExpectedVersion, c)) return false;
                await session.InsertEventAsync(RecallDrillMapping.Event(_support, plan.Id, RecallDrillEventKind.PlanUpdated, now, request.IdempotencyKey, fingerprint), c);
                await session.InsertAuditAsync(_support.Audit(RecallDrillContract.AuditPlanSaved, RecallDrillContract.SubjectPlan, plan.Id, plan.Code, now,
                    request.IdempotencyKey, RecallDrillPlanInputs.Detail(plan, fingerprint)), c);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await ReplayAsync(plan.Id, request.IdempotencyKey, fingerprint, ct) ?? RecallSupport.KeyReused<RecallDrillPlanDto>();
        }

        return saved ? Response<RecallDrillPlanDto>.Success(RecallDrillMapping.Plan(plan)) : RecallSupport.Conflict<RecallDrillPlanDto>();
    }

    private async Task<Response<RecallDrillPlanDto>?> ReplayAsync(Guid planId, string key, string fingerprint, CancellationToken ct)
    {
        if (await _drills.FindEventByKeyAsync(planId, _support.UserId, key, ct) is not { } earlier) return null;
        return earlier.RequestFingerprint == fingerprint && await _drills.FindPlanAsync(planId, ct) is { } current
            ? Response<RecallDrillPlanDto>.Success(RecallDrillMapping.Plan(current))
            : RecallSupport.KeyReused<RecallDrillPlanDto>();
    }
}
