using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;

/*
 * WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — the four stock-behaviour commands. Each one: permissioned by its own
 * constant, tenant- and legal-entity-scope bounded like every product action (an unseen record is a 404), O-8 field by
 * field (StockBehaviourPlan), the record-level rules (StockBehaviourRules; cross-record consistency is S6), then ONE
 * conditional store write that moves the version only while no operation has pinned it (CT decision A). Audited by
 * path b (IAuditableCommand → central audit); no local audit intent (CT decision 2).
 */
public sealed class StockBehaviourContext(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IGlobalProductRepository products,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    TimeProvider clock)
{
    public TimeProvider Clock => clock;
    public IGskuRepository Gskus => gskus;
    public Guid TenantId => tenantContext.TenantId;

    public bool Permitted(string permission) =>
        actorContext.TryResolveCanonicalHumanSubject(out _) && actorContext.HasPermission(permission);

    /// <summary>The GSKU, its revision and product, in this tenant and inside the caller's legal-entity scope.</summary>
    public async Task<(Gsku? Gsku, string? Code, int Status)> ResolveGskuAsync(
        string permission, Guid gskuId, CancellationToken cancellationToken)
    {
        var guard = new ProductLegalEntityScopeConsumerGuard(rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await guard.ResolveContextAsync(permission, cancellationToken);
        if (!scope.IsSuccessful) return (null, scope.FailureCode, scope.StatusCode);
        var gsku = await gskus.GetByIdAsync(gskuId, cancellationToken);
        var revision = gsku is null ? null : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null ? null : await products.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (gsku is null || gsku.TenantId != tenantContext.TenantId || revision is null || product is null)
            return (null, null, 404);
        var decision = await guard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        return decision.Allowed ? (gsku, null, 200) : (null, null, 404);
    }

    public static bool ValidCommand(Guid recordId, int expectedVersion, Guid operationId) =>
        recordId != Guid.Empty && operationId != Guid.Empty && expectedVersion >= 0 && expectedVersion < int.MaxValue;

    /// <summary>The record moved since the caller read it: the same command again is a replay, anything else a conflict.</summary>
    public static Response<StockBehaviourResult>? Stale(
        Guid recordId, int version, int expectedVersion, StockBehaviourCommandMark? mark, Guid operationId, string fingerprint)
    {
        if (mark is not null && mark.OperationId == operationId)
            return string.Equals(mark.Fingerprint, fingerprint, StringComparison.Ordinal)
                ? Response<StockBehaviourResult>.Success(new(recordId, mark.ResultVersion, true, true), 200)
                : Fail(StockBehaviourStoreCodes.IdempotencyConflict, 409);
        return version != expectedVersion ? Fail(StockBehaviourStoreCodes.ConcurrencyConflict, 409) : null;
    }

    public static bool Editable(ProductIdentityLifecycleStatus status) =>
        status is ProductIdentityLifecycleStatus.Draft or ProductIdentityLifecycleStatus.IdentityApproved;

    public static Response<StockBehaviourResult> Done(Guid recordId, StockBehaviourWriteResult written)
    {
        if (written.Succeeded)
            return Response<StockBehaviourResult>.Success(new(recordId, written.Version!.Value, true, written.IsReplay), 200);
        var (status, code) = StockBehaviourPlan.StoreFailure(written.ErrorCode);
        return Fail(code, status);
    }

    public StockBehaviourWrite Write(Guid recordId, int expectedVersion, ProductIdentityLifecycleStatus status,
        StockBehaviourPlan plan, Guid operationId, string fingerprint, bool marksGtin = false) =>
        new(recordId, expectedVersion, status, plan.Sets, plan.FirstFills,
            new StockBehaviourCommandMark(operationId, fingerprint, expectedVersion + 1),
            clock.GetUtcNow().ToUniversalTime(), marksGtin);

    public static Response<StockBehaviourResult> Fail(string code, int status) =>
        Response<StockBehaviourResult>.Fail(code, status);

    public static IEnumerable<string> Units(IEnumerable<ItemUomConversion> conversions) =>
        conversions.SelectMany(conversion => new[] { conversion.FromUom, conversion.ToUom });

    /// <summary>A sent list is never empty (null = not sent); a conversion entry is never null.</summary>
    public static string? ShapeError(
        IReadOnlyList<ItemUomConversion>? conversions, IReadOnlyList<string>? storage, int? shelfLifeDays)
    {
        if (conversions is not null && (conversions.Count == 0 || conversions.Any(c => c is null)))
            return StockBehaviourRules.ConversionInvalid;
        if (storage is not null && (storage.Count == 0 || storage.Any(string.IsNullOrEmpty)))
            return StockBehaviourRules.StorageConditionInvalid;
        return StockBehaviourRules.IsValidShelfLife(shelfLifeDays) ? null : StockBehaviourRules.ShelfLifeInvalid;
    }
}

public sealed class SetGskuStockBehaviourHandler(
    StockBehaviourContext context,
    IItemReferenceResolver references,
    IProductStockBehaviourRepository store)
    : IRequestHandler<SetGskuStockBehaviourCommand, Response<StockBehaviourResult>>
{
    public async Task<Response<StockBehaviourResult>> Handle(
        SetGskuStockBehaviourCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!context.Permitted(StockBehaviourPermissions.EditGsku))
            return StockBehaviourContext.Fail(StockBehaviourErrors.Forbidden, 403);
        if (!StockBehaviourContext.ValidCommand(command.GskuId, command.ExpectedVersion, command.OperationId)
            || command is { BaseUomCode: null, UomConversions: null, ShelfLifeDays: null, StorageConditionCodes: null,
                LotControlled: null, Stockable: null, RetestDays: null, MinRemainingShelfLifeDays: null }
            || command.BaseUomCode is { Length: 0 })
            return StockBehaviourContext.Fail(StockBehaviourErrors.Invalid, 400);
        if (StockBehaviourContext.ShapeError(command.UomConversions, command.StorageConditionCodes, command.ShelfLifeDays)
            is { } shape)
            return StockBehaviourContext.Fail(shape, 422);
        if (!StockBehaviourRules.IsValidOptionalDays(command.RetestDays)
            || !StockBehaviourRules.IsValidOptionalDays(command.MinRemainingShelfLifeDays))
            return StockBehaviourContext.Fail(StockBehaviourRules.DaysInvalid, 422);

        var (gsku, scopeCode, scopeStatus) = await context.ResolveGskuAsync(
            StockBehaviourPermissions.EditGsku, command.GskuId, cancellationToken);
        if (gsku is null) return StockBehaviourContext.Fail(scopeCode ?? "GSKU_NOT_FOUND", scopeStatus);

        var fingerprint = StockBehaviourPlan.Fingerprint("gsku", gsku.Id, command.ExpectedVersion, new
        {
            command.BaseUomCode, command.UomConversions, command.ShelfLifeDays, command.StorageConditionCodes,
            command.LotControlled, command.Stockable, command.RetestDays, command.MinRemainingShelfLifeDays
        });
        if (StockBehaviourContext.Stale(gsku.Id, gsku.Version, command.ExpectedVersion, gsku.StockBehaviourCommand,
                command.OperationId, fingerprint) is { } stale)
            return stale;
        if (!StockBehaviourContext.Editable(gsku.LifecycleStatus))
            return StockBehaviourContext.Fail(StockBehaviourStoreCodes.RecordNotEditable, 409);

        var plan = new StockBehaviourPlan(gsku.LifecycleStatus);
        plan.Field(StockBehaviourFields.BaseUomCode, command.BaseUomCode, gsku.BaseUomCode);
        plan.Field(StockBehaviourFields.UomConversions, command.UomConversions, gsku.UomConversions);
        plan.Field(StockBehaviourFields.ShelfLifeDays, command.ShelfLifeDays, gsku.ShelfLifeDays);
        plan.Field(StockBehaviourFields.StorageConditionCodes, command.StorageConditionCodes, gsku.StorageConditionCodes);
        plan.Field(StockBehaviourFields.LotControlled, command.LotControlled, gsku.LotControlled);
        plan.Field(StockBehaviourFields.Stockable, command.Stockable, gsku.Stockable);
        // S3b: the same O-8 rule — free on a Draft, a first fill on an approved GSKU, locked once filled.
        plan.Field(StockBehaviourFields.RetestDays, command.RetestDays, gsku.RetestDays);
        plan.Field(StockBehaviourFields.MinRemainingShelfLifeDays, command.MinRemainingShelfLifeDays,
            gsku.MinRemainingShelfLifeDays);
        if (plan.LockedField is not null)
            return StockBehaviourContext.Fail(StockBehaviourStoreCodes.FieldLocked, 409);
        // S3b FIX1 (CT decision): the retest interval and the minimum remaining shelf life never exceed the shelf life,
        // when it is known — checked on the record as it will be (a value once filled is locked, so a wrong one would stay).
        if (!StockBehaviourRules.DaysWithinShelfLife(
                plan.After(StockBehaviourFields.ShelfLifeDays, gsku.ShelfLifeDays),
                plan.After(StockBehaviourFields.RetestDays, gsku.RetestDays),
                plan.After(StockBehaviourFields.MinRemainingShelfLifeDays, gsku.MinRemainingShelfLifeDays)))
            return StockBehaviourContext.Fail(StockBehaviourRules.DaysInvalid, 422);

        var baseUom = plan.After(StockBehaviourFields.BaseUomCode, gsku.BaseUomCode);
        var conversions = plan.After<List<ItemUomConversion>>(StockBehaviourFields.UomConversions, gsku.UomConversions);
        var storage = plan.After<List<string>>(StockBehaviourFields.StorageConditionCodes, gsku.StorageConditionCodes);
        var unitsChange = plan.Sets.ContainsKey(StockBehaviourFields.BaseUomCode)
                          || plan.Sets.ContainsKey(StockBehaviourFields.UomConversions);
        var storageChanges = plan.Sets.ContainsKey(StockBehaviourFields.StorageConditionCodes);
        if (unitsChange || storageChanges)
        {
            // Conversions are measured from the base unit: none can be checked before it is set.
            if (unitsChange && baseUom is null)
                return StockBehaviourContext.Fail(StockBehaviourRules.ConversionInvalid, 422);
            // O-14 / CT decision (a): the box ↔ pack edge is the pack's, whatever its unit; never entered.
            var edges = new List<ItemUomConversion>(conversions ?? [])
            {
                StockBehaviourRules.DerivedPackConversion(gsku.PackQuantity, gsku.PackUomCode)
            };
            var uoms = baseUom is null ? [] : StockBehaviourContext.Units(edges).Append(baseUom).ToList();
            var (answer, status, code) = await StockReferenceCheck.ResolveAsync(references,
                unitsChange ? uoms : [], storageChanges ? storage! : [], gsku.StockReferenceSelections ?? [],
                gsku.PackUomCode, cancellationToken);
            if (answer is null) return StockBehaviourContext.Fail(code!, status);
            if (unitsChange && StockBehaviourRules.ValidateConversions(baseUom!, edges, answer.Uoms) is { } conversionError)
                return StockBehaviourContext.Fail(conversionError, 422);
            if (storageChanges && StockBehaviourRules.ValidateStorageConditions(
                    storage!, answer.StorageConditions, answer.Temperatures) is { } storageError)
                return StockBehaviourContext.Fail(storageError, 422);
            plan.Sets[StockBehaviourFields.StockReferenceSelections] = StockReferenceCheck.ProofsFor(answer, uoms, storage ?? []);
        }

        // O-8 / O-25: Stockable true only with every required field filled (a GSKU is never packaging).
        if (plan.After(StockBehaviourFields.Stockable, gsku.Stockable) == true
            && !StockBehaviourRules.GskuIsComplete(baseUom,
                plan.After(StockBehaviourFields.ShelfLifeDays, gsku.ShelfLifeDays), storage,
                plan.After(StockBehaviourFields.LotControlled, gsku.LotControlled)))
            return StockBehaviourContext.Fail(StockBehaviourRules.Incomplete, 422);

        if (plan.Sets.Count == 0)
            return Response<StockBehaviourResult>.Success(new(gsku.Id, gsku.Version, false, false), 200);
        return StockBehaviourContext.Done(gsku.Id, await store.WriteGskuAsync(
            context.Write(gsku.Id, command.ExpectedVersion, gsku.LifecycleStatus, plan, command.OperationId, fingerprint),
            cancellationToken));
    }
}

public sealed class SetLskuStockBehaviourHandler(
    StockBehaviourContext context,
    ILskuRepository lskus,
    IProductStockBehaviourRepository store)
    : IRequestHandler<SetLskuStockBehaviourCommand, Response<StockBehaviourResult>>
{
    public async Task<Response<StockBehaviourResult>> Handle(
        SetLskuStockBehaviourCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!context.Permitted(StockBehaviourPermissions.EditLsku))
            return StockBehaviourContext.Fail(StockBehaviourErrors.Forbidden, 403);
        if (!StockBehaviourContext.ValidCommand(command.LskuId, command.ExpectedVersion, command.OperationId)
            || command.SerialControlled is null)
            return StockBehaviourContext.Fail(StockBehaviourErrors.Invalid, 400);

        var lsku = await lskus.GetByIdAsync(command.LskuId, cancellationToken);
        if (lsku is null || lsku.TenantId != context.TenantId) return StockBehaviourContext.Fail("LSKU_NOT_FOUND", 404);
        var (gsku, scopeCode, scopeStatus) = await context.ResolveGskuAsync(
            StockBehaviourPermissions.EditLsku, lsku.GskuId, cancellationToken);
        if (gsku is null) return StockBehaviourContext.Fail(scopeCode ?? "LSKU_NOT_FOUND", scopeStatus);

        var fingerprint = StockBehaviourPlan.Fingerprint("lsku", lsku.Id, command.ExpectedVersion,
            new { command.SerialControlled });
        if (StockBehaviourContext.Stale(lsku.Id, lsku.Version, command.ExpectedVersion, lsku.StockBehaviourCommand,
                command.OperationId, fingerprint) is { } stale)
            return stale;
        if (!StockBehaviourContext.Editable(lsku.LifecycleStatus))
            return StockBehaviourContext.Fail(StockBehaviourStoreCodes.RecordNotEditable, 409);

        var plan = new StockBehaviourPlan(lsku.LifecycleStatus);
        plan.Field(StockBehaviourFields.SerialControlled, command.SerialControlled, lsku.SerialControlled);
        if (plan.LockedField is not null)
            return StockBehaviourContext.Fail(StockBehaviourStoreCodes.FieldLocked, 409);
        if (plan.Sets.Count == 0)
            return Response<StockBehaviourResult>.Success(new(lsku.Id, lsku.Version, false, false), 200);
        return StockBehaviourContext.Done(lsku.Id, await store.WriteLskuAsync(
            context.Write(lsku.Id, command.ExpectedVersion, lsku.LifecycleStatus, plan, command.OperationId, fingerprint),
            cancellationToken));
    }
}

public sealed class SetFinishedGoodStockBehaviourHandler(
    StockBehaviourContext context,
    IFinishedGoodRepository finishedGoods,
    IItemReferenceResolver references,
    IProductStockBehaviourRepository store)
    : IRequestHandler<SetFinishedGoodStockBehaviourCommand, Response<StockBehaviourResult>>
{
    public async Task<Response<StockBehaviourResult>> Handle(
        SetFinishedGoodStockBehaviourCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!context.Permitted(StockBehaviourPermissions.EditFinishedGood))
            return StockBehaviourContext.Fail(StockBehaviourErrors.Forbidden, 403);
        if (!StockBehaviourContext.ValidCommand(command.FinishedGoodId, command.ExpectedVersion, command.OperationId)
            || command is { UomConversions: null, ShelfLifeDays: null, StorageConditionCodes: null, LotControlled: null,
                SerialControlled: null, Stockable: null })
            return StockBehaviourContext.Fail(StockBehaviourErrors.Invalid, 400);
        if (StockBehaviourContext.ShapeError(command.UomConversions, command.StorageConditionCodes, command.ShelfLifeDays)
            is { } shape)
            return StockBehaviourContext.Fail(shape, 422);

        var finishedGood = await finishedGoods.GetByIdAsync(command.FinishedGoodId, cancellationToken);
        if (finishedGood is null || finishedGood.TenantId != context.TenantId)
            return StockBehaviourContext.Fail("FINISHED_GOOD_NOT_FOUND", 404);
        var (gsku, scopeCode, scopeStatus) = await context.ResolveGskuAsync(
            StockBehaviourPermissions.EditFinishedGood, finishedGood.GskuId, cancellationToken);
        if (gsku is null) return StockBehaviourContext.Fail(scopeCode ?? "FINISHED_GOOD_NOT_FOUND", scopeStatus);

        var fingerprint = StockBehaviourPlan.Fingerprint("finished-good", finishedGood.Id, command.ExpectedVersion, new
        {
            command.UomConversions, command.ShelfLifeDays, command.StorageConditionCodes, command.LotControlled,
            command.SerialControlled, command.Stockable
        });
        if (StockBehaviourContext.Stale(finishedGood.Id, finishedGood.Version, command.ExpectedVersion,
                finishedGood.StockBehaviourCommand, command.OperationId, fingerprint) is { } stale)
            return stale;
        if (!StockBehaviourContext.Editable(finishedGood.LifecycleStatus) || finishedGood.DraftCancellation is not null)
            return StockBehaviourContext.Fail(StockBehaviourStoreCodes.RecordNotEditable, 409);

        var plan = new StockBehaviourPlan(finishedGood.LifecycleStatus);
        plan.Field(StockBehaviourFields.UomConversions, command.UomConversions, finishedGood.UomConversions);
        plan.Field(StockBehaviourFields.ShelfLifeDays, command.ShelfLifeDays, finishedGood.ShelfLifeDays);
        plan.Field(StockBehaviourFields.StorageConditionCodes, command.StorageConditionCodes, finishedGood.StorageConditionCodes);
        plan.Field(StockBehaviourFields.LotControlled, command.LotControlled, finishedGood.LotControlled);
        plan.Field(StockBehaviourFields.SerialControlled, command.SerialControlled, finishedGood.SerialControlled);
        plan.Field(StockBehaviourFields.Stockable, command.Stockable, finishedGood.Stockable);
        if (plan.LockedField is not null)
            return StockBehaviourContext.Fail(StockBehaviourStoreCodes.FieldLocked, 409);

        // O-15: an own shelf life is measured against the GSKU's; with the GSKU's still empty it cannot be, so it waits
        // for it (the GSKU's, once filled on an approved GSKU, is locked by O-8 — it can never later fall below this one).
        if (plan.Sets.ContainsKey(StockBehaviourFields.ShelfLifeDays))
        {
            if (gsku.ShelfLifeDays is null) return StockBehaviourContext.Fail(StockBehaviourErrors.GskuShelfLifeMissing, 409);
            if (!StockBehaviourRules.FinishedGoodShelfLifeWithinGsku(command.ShelfLifeDays, gsku.ShelfLifeDays))
                // CT decision (pack §13 > prompt): 422 — a value invalid against another record's, not a state conflict.
                return StockBehaviourContext.Fail(StockBehaviourRules.ShelfLifeExceedsGsku, 422);
        }

        var conversions = plan.After<List<ItemUomConversion>>(StockBehaviourFields.UomConversions, finishedGood.UomConversions);
        var storage = plan.After<List<string>>(StockBehaviourFields.StorageConditionCodes, finishedGood.StorageConditionCodes);
        var unitsChange = plan.Sets.ContainsKey(StockBehaviourFields.UomConversions);
        var storageChanges = plan.Sets.ContainsKey(StockBehaviourFields.StorageConditionCodes);
        if (unitsChange || storageChanges)
        {
            // O-14: the Finished Good's base unit is its GSKU's; its chain (§4.4 rule 7) runs through the GSKU's edges.
            if (unitsChange && gsku.BaseUomCode is null)
                return StockBehaviourContext.Fail(StockBehaviourErrors.GskuBaseUomMissing, 409);
            var edges = new List<ItemUomConversion>(gsku.UomConversions ?? [])
            {
                StockBehaviourRules.DerivedPackConversion(gsku.PackQuantity, gsku.PackUomCode)
            };
            edges.AddRange(conversions ?? []);
            var uoms = unitsChange ? StockBehaviourContext.Units(edges).Append(gsku.BaseUomCode!).ToList() : [];
            var held = (finishedGood.StockReferenceSelections ?? []).Concat(gsku.StockReferenceSelections ?? []).ToList();
            var (answer, status, code) = await StockReferenceCheck.ResolveAsync(references, uoms,
                storageChanges ? storage! : [], held, gsku.PackUomCode, cancellationToken);
            if (answer is null) return StockBehaviourContext.Fail(code!, status);
            if (unitsChange && StockBehaviourRules.ValidateConversions(gsku.BaseUomCode!, edges, answer.Uoms) is { } conversionError)
                return StockBehaviourContext.Fail(conversionError, 422);
            if (storageChanges && StockBehaviourRules.ValidateStorageConditions(
                    storage!, answer.StorageConditions, answer.Temperatures) is { } storageError)
                return StockBehaviourContext.Fail(storageError, 422);
            plan.Sets[StockBehaviourFields.StockReferenceSelections] = StockReferenceCheck.ProofsFor(
                answer, StockBehaviourContext.Units(conversions ?? []), storage ?? []);
        }

        if (plan.After(StockBehaviourFields.Stockable, finishedGood.Stockable) == true
            && !StockBehaviourRules.FinishedGoodIsComplete(gsku.BaseUomCode,
                plan.After(StockBehaviourFields.ShelfLifeDays, finishedGood.ShelfLifeDays) ?? gsku.ShelfLifeDays,
                conversions, storage,
                plan.After(StockBehaviourFields.LotControlled, finishedGood.LotControlled),
                plan.After(StockBehaviourFields.SerialControlled, finishedGood.SerialControlled),
                finishedGood.Gtin))
            return StockBehaviourContext.Fail(StockBehaviourRules.Incomplete, 422);

        if (plan.Sets.Count == 0)
            return Response<StockBehaviourResult>.Success(new(finishedGood.Id, finishedGood.Version, false, false), 200);
        return StockBehaviourContext.Done(finishedGood.Id, await store.WriteFinishedGoodAsync(
            context.Write(finishedGood.Id, command.ExpectedVersion, finishedGood.LifecycleStatus, plan,
                command.OperationId, fingerprint),
            cancellationToken));
    }
}

public sealed class AssignFinishedGoodGtinHandler(
    StockBehaviourContext context,
    IFinishedGoodRepository finishedGoods,
    IProductStockBehaviourRepository store)
    : IRequestHandler<AssignFinishedGoodGtinCommand, Response<StockBehaviourResult>>
{
    public async Task<Response<StockBehaviourResult>> Handle(
        AssignFinishedGoodGtinCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!context.Permitted(StockBehaviourPermissions.EditFinishedGood))
            return StockBehaviourContext.Fail(StockBehaviourErrors.Forbidden, 403);
        if (!StockBehaviourContext.ValidCommand(command.FinishedGoodId, command.ExpectedVersion, command.OperationId)
            || command.Gtin is null)
            return StockBehaviourContext.Fail(StockBehaviourErrors.Invalid, 400);
        if (!StockBehaviourRules.IsValidGtin14(command.Gtin))
            return StockBehaviourContext.Fail(StockBehaviourRules.GtinInvalid, 422);

        var finishedGood = await finishedGoods.GetByIdAsync(command.FinishedGoodId, cancellationToken);
        if (finishedGood is null || finishedGood.TenantId != context.TenantId)
            return StockBehaviourContext.Fail("FINISHED_GOOD_NOT_FOUND", 404);
        var (gsku, scopeCode, scopeStatus) = await context.ResolveGskuAsync(
            StockBehaviourPermissions.EditFinishedGood, finishedGood.GskuId, cancellationToken);
        if (gsku is null) return StockBehaviourContext.Fail(scopeCode ?? "FINISHED_GOOD_NOT_FOUND", scopeStatus);

        var fingerprint = StockBehaviourPlan.Fingerprint("finished-good-gtin", finishedGood.Id, command.ExpectedVersion,
            new { command.Gtin });
        if (StockBehaviourContext.Stale(finishedGood.Id, finishedGood.Version, command.ExpectedVersion,
                finishedGood.GtinCommand, command.OperationId, fingerprint) is { } stale)
            return stale;
        if (!StockBehaviourContext.Editable(finishedGood.LifecycleStatus) || finishedGood.DraftCancellation is not null)
            return StockBehaviourContext.Fail(StockBehaviourStoreCodes.RecordNotEditable, 409);

        var plan = new StockBehaviourPlan(finishedGood.LifecycleStatus);
        plan.Field(StockBehaviourFields.Gtin, command.Gtin, finishedGood.Gtin);
        if (plan.LockedField is not null)
            return StockBehaviourContext.Fail(StockBehaviourStoreCodes.FieldLocked, 409);
        if (plan.Sets.Count == 0)
            return Response<StockBehaviourResult>.Success(new(finishedGood.Id, finishedGood.Version, false, false), 200);
        // R4: named before the write; the unique partial index refuses the race between two Finished Goods.
        if (await store.GtinHeldByAnotherAsync(command.Gtin, finishedGood.Id, cancellationToken))
            return StockBehaviourContext.Fail(StockBehaviourStoreCodes.GtinConflict, 409);
        return StockBehaviourContext.Done(finishedGood.Id, await store.WriteFinishedGoodAsync(
            context.Write(finishedGood.Id, command.ExpectedVersion, finishedGood.LifecycleStatus, plan,
                command.OperationId, fingerprint, marksGtin: true),
            cancellationToken));
    }
}
