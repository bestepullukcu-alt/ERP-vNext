using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Validators;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Domain.Repositories.Commercial;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders.Handlers.CommandHandlers;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 command handlers (§12, §13). Shared reads of the outside (accounts, SKUs) live in
/// <see cref="PreOrderParts"/>; every state change goes through <see cref="PreOrderStateMachine"/>; every write is an
/// optimistic replace (a stale version is 409, never a silent overwrite).
/// </summary>
public sealed class PreOrderParts
{
    private readonly IAccountRepository _accounts;
    private readonly IPreOrderMdmDirectory _mdm;

    public PreOrderParts(IAccountRepository accounts, IPreOrderMdmDirectory mdm)
    {
        _accounts = accounts;
        _mdm = mdm;
    }

    /// <summary>The pharmacy (tenant's account of type pharmacy) as a snapshot; 400 otherwise.</summary>
    public async Task<(PreOrderParty? Party, Response<T>? Error)> PharmacyAsync<T>(Guid tenantId, Guid accountId, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(tenantId, accountId, ct);
        return account is null || account.TenantId != tenantId || account.IsDeleted || !PreOrderAccountTypes.IsPharmacy(account.AccountType)
            ? (null, PreOrderResults.Fail<T>(PreOrderErrors.PharmacyInvalid, "The pharmacy is not a pharmacy account of this tenant.", 400))
            : (Party(account), null);
    }

    /// <summary>The depot (a distributor / wholesaler account, K-W13) as a snapshot; 400 otherwise.</summary>
    public async Task<(PreOrderParty? Party, Response<T>? Error)> DepotAsync<T>(Guid tenantId, Guid accountId, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(tenantId, accountId, ct);
        return account is null || account.TenantId != tenantId || account.IsDeleted || !PreOrderAccountTypes.IsDepot(account.AccountType)
            ? (null, PreOrderResults.Fail<T>(PreOrderErrors.DepotInvalid, "The depot is not a distributor / wholesaler account of this tenant.", 400))
            : (Party(account), null);
    }

    /// <summary>MOD-0149 carries no GLN, pharmacy code or tax number yet (W6-BE-a measurement) — those stay null.</summary>
    private static PreOrderParty Party(Domain.Entities.Account a) => new()
    {
        AccountId = a.Id, Name = a.AccountName, AccountCode = a.AccountCode, AccountType = a.AccountType, CityRef = a.CityRef, Address = a.AddressLine,
        Email = PreOrderResults.Clean(a.ResponsiblePersonEmail)
    };

    /// <summary>The lines with their SKU snapshot from product-master v1.1: only an ACTIVE SKU enters (Retired → 400
    /// <c>pre_order_sku_retired</c>, any other / absent → 400 <c>pre_order_sku_not_found</c>); MDM down → 503.</summary>
    public async Task<(List<PreOrderLine>? Lines, Response<T>? Error)> LinesAsync<T>(IReadOnlyList<PreOrderLineInput> inputs, CancellationToken ct)
    {
        if (inputs.Count == 0)
        {
            return (new List<PreOrderLine>(), null);
        }

        var skus = await _mdm.ReadSkusAsync(inputs.Select(i => i.SkuId!.Value).ToList(), ct);
        if (skus is null)
        {
            return (null, PreOrderResults.ContextUnavailable<T>("The product master (MDM)"));
        }

        var lines = new List<PreOrderLine>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (!skus.TryGetValue(input.SkuId!.Value, out var sku))
            {
                return (null, PreOrderResults.Fail<T>(PreOrderErrors.SkuNotFound, $"Line {i + 1}: the SKU does not exist.", 400));
            }

            if (sku.IsRetired)
            {
                return (null, PreOrderResults.Fail<T>(PreOrderErrors.SkuRetired, $"Line {i + 1}: {sku.Code} is Retired.", 400));
            }

            if (!sku.IsActive)
            {
                return (null, PreOrderResults.Fail<T>(PreOrderErrors.SkuNotFound, $"Line {i + 1}: {sku.Code} is not orderable ({sku.LifecycleStatus}).", 400));
            }

            lines.Add(new PreOrderLine
            {
                LineNo = i + 1, SkuId = sku.SkuId, SkuLevel = sku.SkuLevel, Quantity = input.Quantity!.Value,
                Mf = input.Mf is { } mf ? new PreOrderMf { Buy = mf.Buy, Free = mf.Free } : null, DiscountPct = input.DiscountPct,
                Sku = new PreOrderSkuSnapshot
                {
                    Code = sku.Code, Name = sku.Name, PackQuantity = sku.PackQuantity, PackUomCode = sku.PackUomCode, Gtin = sku.Gtin
                }
            });
        }

        return (lines, null);
    }

    public static bool SameLines(IReadOnlyList<PreOrderLine> a, IReadOnlyList<PreOrderLine> b)
        => a.Count == b.Count && a.Zip(b).All(x => x.First.SkuId == x.Second.SkuId && x.First.Quantity == x.Second.Quantity
                                                 && x.First.Mf?.Buy == x.Second.Mf?.Buy && x.First.Mf?.Free == x.Second.Mf?.Free
                                                 && x.First.DiscountPct == x.Second.DiscountPct);
}

public sealed class PreOrderCommandHandlers :
    IRequestHandler<CreatePreOrderCommand, Response<PreOrderDto>>,
    IRequestHandler<UpdatePreOrderCommand, Response<PreOrderDto>>,
    IRequestHandler<DiscardPreOrderCommand, Response<PreOrderDto>>,
    IRequestHandler<CapturePreOrderSignatureCommand, Response<PreOrderDto>>,
    IRequestHandler<SubmitPreOrderCommand, Response<PreOrderDto>>,
    IRequestHandler<DecidePreOrderCommand, Response<PreOrderDto>>,
    IRequestHandler<CancelPreOrderCommand, Response<PreOrderDto>>,
    IRequestHandler<CopyPreOrderCommand, Response<PreOrderDto>>,
    IRequestHandler<TransmitPreOrderCommand, Response<PreOrderDto>>,
    IRequestHandler<RecordPreOrderDepotResponseCommand, Response<PreOrderDto>>,
    IRequestHandler<UpdatePreOrderSettingsCommand, Response<PreOrderSettingsDto>>
{
    private readonly PreOrderContext _c;
    private readonly PreOrderParts _parts;
    private readonly PreOrderPricing _pricing;
    private readonly PreOrderApprovalService _approval;
    private readonly PreOrderTransmissionService _transmission;
    private readonly IPreOrderNotifier _notifier;
    private readonly IPreOrderCodeSequenceRepository _sequences;
    private readonly IVisitEvidenceStore _evidence;

    public PreOrderCommandHandlers(PreOrderContext context, PreOrderParts parts, PreOrderPricing pricing, PreOrderApprovalService approval,
        PreOrderTransmissionService transmission, IPreOrderNotifier notifier, IPreOrderCodeSequenceRepository sequences, IVisitEvidenceStore evidence)
    {
        _c = context;
        _parts = parts;
        _pricing = pricing;
        _approval = approval;
        _transmission = transmission;
        _notifier = notifier;
        _sequences = sequences;
        _evidence = evidence;
    }

    // ── draft ────────────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<PreOrderDto>> Handle(CreatePreOrderCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderDto>("Tenant context is required.");
        }

        if (PreOrderValidator.Check(request.Input, create: true, _c.Today) is { } problem)
        {
            return PreOrderResults.Fail<PreOrderDto>(problem.Code, problem.Message, 400);
        }

        var (seller, sellerError) = await _c.SellerAsync<PreOrderDto>(tenantId, ct);
        if (sellerError is not null)
        {
            return sellerError;
        }

        if (seller!.Prefix is null)
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.NoCompanyPrefix,
                $"No ORD prefix is set for {seller.LegalEntity.Code} (pre-order settings).", 422);
        }

        var order = new PreOrder { TenantId = tenantId, CreatedBy = _c.Actor.ActorName, CreatedAt = _c.Now };
        if (await FillAsync(order, request.Input, seller.LegalEntity, tenantId, ct) is { } fillError)
        {
            return fillError;
        }

        order.Seller = new PreOrderSeller
        {
            LegalEntityId = seller.LegalEntity.Id, Prefix = seller.Prefix, Code = seller.LegalEntity.Code, Name = seller.LegalEntity.Name
        };
        order.CountryCode = seller.LegalEntity.CountryCode.ToUpperInvariant();
        order.LocalCurrencyCode = seller.LegalEntity.BaseCurrencyCode.ToUpperInvariant();
        order.Rep = new PreOrderRep
        {
            UserId = _c.CallerId!.Value, DisplayName = await _c.DisplayNameAsync(_c.CallerId.Value, ct), PositionId = seller.PositionId,
            PositionCode = seller.PositionCode
        };
        order.Code = $"{seller.Prefix}-ORD-{await _sequences.NextAsync(tenantId, seller.Prefix, ct):000000}";
        PreOrderStateMachine.Append(order, PreOrderEventTypes.Created, _c.CallerText, _c.Now, new Dictionary<string, string?> { ["code"] = order.Code });
        await _c.Orders.InsertAsync(order, ct);
        return Response<PreOrderDto>.Success(_c.Dto(order, await _c.VisibilityAsync(tenantId, ct)), 201);
    }

    public async Task<Response<PreOrderDto>> Handle(UpdatePreOrderCommand request, CancellationToken ct)
    {
        var (order, visibility, error) = await OwnDraftAsync(request.Id, request.Input?.ExpectedVersion, ct);
        if (error is not null)
        {
            return error;
        }

        if (PreOrderValidator.Check(request.Input, create: true, _c.Today) is { } problem)
        {
            return PreOrderResults.Fail<PreOrderDto>(problem.Code, problem.Message, 400);
        }

        var (outcome, legalEntity) = await _c.Mdm.GetLegalEntityAsync(order!.Seller.LegalEntityId, ct);
        if (outcome != PreOrderLookupOutcome.Found)
        {
            return PreOrderResults.ContextUnavailable<PreOrderDto>("The legal entity (MDM)");
        }

        var expected = order.Version;
        var before = order.Lines.ToList();
        var pharmacyBefore = order.Pharmacy.AccountId;
        if (await FillAsync(order, request.Input!, legalEntity!, order.TenantId, ct) is { } fillError)
        {
            return fillError;
        }

        if (order.Signature is not null && (!PreOrderParts.SameLines(before, order.Lines) || pharmacyBefore != order.Pharmacy.AccountId))
        {
            order.Signature = null; // the pharmacist signed other lines
            PreOrderStateMachine.Append(order, PreOrderEventTypes.SignatureCleared, _c.CallerText, _c.Now);
        }

        PreOrderStateMachine.Append(order, PreOrderEventTypes.Updated, _c.CallerText, _c.Now);
        return await _c.SaveAsync(order, expected, ct) ? Response<PreOrderDto>.Success(_c.Dto(order, visibility!)) : PreOrderResults.Stale<PreOrderDto>();
    }

    /// <summary>The draft's editable part from the input (full replace): pharmacy, depot, date, note, visit, lines; the price
    /// mode follows the seller's list in force today (a discount in quantity mode is 400).</summary>
    private async Task<Response<PreOrderDto>?> FillAsync(PreOrder order, PreOrderInput input, PreOrderLegalEntity seller, Guid tenantId,
        CancellationToken ct)
    {
        var (pharmacy, pharmacyError) = await _parts.PharmacyAsync<PreOrderDto>(tenantId, input.PharmacyAccountId!.Value, ct);
        if (pharmacyError is not null)
        {
            return pharmacyError;
        }

        PreOrderParty? depot = null;
        if (input.DepotAccountId is { } depotId)
        {
            var (party, depotError) = await _parts.DepotAsync<PreOrderDto>(tenantId, depotId, ct);
            if (depotError is not null)
            {
                return depotError;
            }

            depot = party;
        }

        var settings = await _c.SettingsAsync(tenantId, ct);
        var mode = await _pricing.ModeAsync(tenantId, seller, _c.Today, PreOrderContext.TierFor(settings, seller.CountryCode), ct);
        var lineInputs = input.Lines ?? Array.Empty<PreOrderLineInput>();
        if (mode.Mode != PricingModes.Priced && lineInputs.Any(l => l.DiscountPct is not null))
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.DiscountNotAllowed, "No price list is in force: a discount cannot be entered.", 400);
        }

        var (lines, linesError) = await _parts.LinesAsync<PreOrderDto>(lineInputs, ct);
        if (linesError is not null)
        {
            return linesError;
        }

        order.Pharmacy = pharmacy!;
        order.Depot = depot;
        order.RequestedDeliveryDate = input.RequestedDeliveryDate;
        order.Note = PreOrderResults.Clean(input.Note);
        order.VisitId = input.VisitId is { } visit && visit != Guid.Empty ? visit : null;
        order.Lines = lines!;
        order.PriceMode = mode.Mode == PricingModes.Priced ? PreOrderPriceModes.Priced : PreOrderPriceModes.Quantity;
        order.Currency = mode.PriceList?.Currency;
        return null;
    }

    public async Task<Response<PreOrderDto>> Handle(DiscardPreOrderCommand request, CancellationToken ct)
    {
        var (order, visibility, error) = await OwnDraftAsync(request.Id, request.Input?.ExpectedVersion, ct);
        if (error is not null)
        {
            return error;
        }

        var expected = order!.Version;
        PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.Discard, PreOrderEventTypes.Discarded, _c.CallerText, _c.Now);
        return await _c.SaveAsync(order, expected, ct) ? Response<PreOrderDto>.Success(_c.Dto(order, visibility!)) : PreOrderResults.Stale<PreOrderDto>();
    }

    public async Task<Response<PreOrderDto>> Handle(CapturePreOrderSignatureCommand request, CancellationToken ct)
    {
        var (order, visibility, error) = await OwnDraftAsync(request.Id, request.Input?.ExpectedVersion, ct);
        if (error is not null)
        {
            return error;
        }

        if (PreOrderValidator.CheckSignature(request.Input, out var image, out var mediaType) is { } problem)
        {
            return PreOrderResults.Fail<PreOrderDto>(problem.Code, problem.Message, 400);
        }

        ContentArtifactStoreResult stored;
        try
        {
            stored = await _evidence.StoreAsync(new VisitEvidenceStoreRequest(order!.Id, Guid.NewGuid(),
                mediaType == "image/png" ? "pre-order-signature.png" : "pre-order-signature.jpg", mediaType, image), ct);
        }
        catch (ContentArtifactStoreException ex)
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.StoreUnavailable, $"The signature could not be stored ({ex.StatusCode}).",
                ex.StatusCode is >= 400 and < 500 and not 408 and not 429 ? 400 : 503);
        }

        var expected = order.Version;
        order.Signature = new PreOrderSignature
        {
            BinaryRef = stored.ContentId, SignerName = PreOrderResults.Clean(request.Input!.SignerName)!, SignedAt = _c.Now, CapturedByUserId = _c.CallerId!.Value
        };
        PreOrderStateMachine.Append(order, PreOrderEventTypes.SignatureCaptured, _c.CallerText, _c.Now);
        if (await _c.SaveAsync(order, expected, ct))
        {
            return Response<PreOrderDto>.Success(_c.Dto(order, visibility!));
        }

        await _evidence.CompensateAsync(stored.ContentId, ct);
        return PreOrderResults.Stale<PreOrderDto>();
    }

    // ── submission ───────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<PreOrderDto>> Handle(SubmitPreOrderCommand request, CancellationToken ct)
    {
        var (order, visibility, error) = await OwnDraftAsync(request.Id, request.Input?.ExpectedVersion, ct, PreOrderStateMachine.Actions.Submit);
        if (error is not null)
        {
            return error;
        }

        var tenantId = order!.TenantId;
        if (order.Lines.Count is 0 or > PreOrderLimits.MaxLines)
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.LinesInvalid, "A pre-order is sent with 1–50 lines.", 400);
        }

        if (order.Depot is null)
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.DepotRequired, "Choose the depot the order goes to.", 400);
        }

        if (order.Signature is null)
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.SignatureRequired, "The pharmacist's signature is required.", 422);
        }

        // the parties and the SKUs are read again: the snapshot is the one of the submission
        var (pharmacy, pharmacyError) = await _parts.PharmacyAsync<PreOrderDto>(tenantId, order.Pharmacy.AccountId, ct);
        if (pharmacyError is not null)
        {
            return pharmacyError;
        }

        var (depot, depotError) = await _parts.DepotAsync<PreOrderDto>(tenantId, order.Depot.AccountId, ct);
        if (depotError is not null)
        {
            return depotError;
        }

        if (depot!.Email is null)
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.DepotNoEmail, "The depot account has no e-mail address to send the order to.", 422);
        }

        var (fresh, linesError) = await _parts.LinesAsync<PreOrderDto>(
            order.Lines.Select(l => new PreOrderLineInput(l.SkuId, l.Quantity, l.Mf is { } m ? new PreOrderMfInput(m.Buy, m.Free) : null, l.DiscountPct)).ToList(),
            ct);
        if (linesError is not null)
        {
            return linesError;
        }

        var settings = await _c.SettingsAsync(tenantId, ct);
        var tier = PreOrderContext.TierFor(settings, order.CountryCode);
        var today = _c.Today;
        var result = await _pricing.EvaluateAsync(tenantId, order.Seller.LegalEntityId, order.CountryCode, today, tier, fresh!, ct);
        if (result.Mode != PricingModes.Priced && fresh!.Any(l => l.DiscountPct is not null))
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.DiscountNotAllowed, "No price list is in force: remove the discounts.", 400);
        }

        if (result.Outcome == PricingOutcomes.Hard)
        {
            var errors = new List<string> { PreOrderErrors.HardLimit, "A line is beyond a hard limit; the order cannot be sent." };
            errors.AddRange(result.Lines.SelectMany(l => l.Findings.Where(f => f.Severity == PricingFindingSeverities.Hard)
                .Select(f => $"L{l.LineNo}:{f.Code}")));
            return Response<PreOrderDto>.Fail(errors, 422);
        }

        // D12: the price, the rate and the outcome are fixed NOW
        var expected = order.Version;
        var now = _c.Now;
        order.Pharmacy = pharmacy!;
        order.Depot = depot;
        order.Lines = fresh!;
        PreOrderPricing.Apply(order, result);
        order.Price = result.PriceList is { } list
            ? new PreOrderPriceSnapshot { PriceListId = list.Id, PriceListVersion = list.Version, Name = list.Name, Kind = list.Kind, ValidFrom = list.ValidFrom, Tier = tier }
            : null;
        order.Evaluation = new PreOrderEvaluationSnapshot { Outcome = result.Outcome, EvaluatedAt = now, RuleSetFingerprint = result.RuleSetFingerprint, Tier = tier };
        order.Fx = await _pricing.FxAsync(tenantId, order.Seller.LegalEntityId, order.Currency, order.LocalCurrencyCode, order.Totals?.Gross, today, ct);
        order.SubmissionNo++;
        PreOrderStateMachine.Append(order, PreOrderEventTypes.Submitted, _c.CallerText, now,
            new Dictionary<string, string?> { ["outcome"] = result.Outcome, ["submission"] = order.SubmissionNo.ToString() });

        // D1: always → the manager; auto-within-limits → only a soft finding needs the manager
        var reasons = new List<string>();
        if (settings.ApprovalMode != PreOrderApprovalModes.AutoWithinLimits)
        {
            reasons.Add(PreOrderApprovalReasons.TenantSetting);
        }

        if (result.Outcome == PricingOutcomes.Soft)
        {
            reasons.Add(PreOrderApprovalReasons.SoftExceeded);
        }

        if (reasons.Count == 0)
        {
            order.ApprovalRequirement = new PreOrderApprovalRequirement { Required = false };
            order.Approval = new PreOrderApproval
            {
                SubmissionNo = order.SubmissionNo, Decision = PreOrderDecisions.Approve, DecidedAt = now, Auto = true, Channel = PreOrderApprovalChannels.Auto
            };
            PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.AutoApprove, PreOrderEventTypes.AutoApproved, null, now);
            if (!await _c.SaveAsync(order, expected, ct))
            {
                return PreOrderResults.Stale<PreOrderDto>();
            }

            await _notifier.AutoApprovedAsync(order, ct);
            await _transmission.TransmitAsync(order, null, ct);
            return Response<PreOrderDto>.Success(_c.Dto(await _c.Orders.GetByIdAsync(tenantId, order.Id, ct) ?? order, visibility!));
        }

        var managers = await _approval.ApproversOfAsync(order.Rep.UserId, ct);
        if (managers.Available && managers.Managers.Count == 0)
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.NoApprover, "No approver was found above the rep's seat.", 422);
        }

        order.ApprovalRequirement = PreOrderApprovalService.Requirement(reasons, managers);
        if (!managers.Available)
        {
            // EK KURAL (CT 2026-10-20): Platform down never refuses the submission — the order waits, approvers are resolved on read
            order.Approval = new PreOrderApproval { SubmissionNo = order.SubmissionNo };
            PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.RequestApproval, PreOrderEventTypes.ApproverUnresolved, _c.CallerText, now);
            return await _c.SaveAsync(order, expected, ct) ? Response<PreOrderDto>.Success(_c.Dto(order, visibility!)) : PreOrderResults.Stale<PreOrderDto>();
        }

        if (await _approval.RequestAsync(order, expected, settings, visibility!, ct) is { } refused)
        {
            return refused;
        }

        return Response<PreOrderDto>.Success(_c.Dto(order, visibility!));
    }

    // ── decision ─────────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<PreOrderDto>> Handle(DecidePreOrderCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderDto>("Tenant context is required.");
        }

        if (PreOrderValidator.CheckDecision(request.Input, out var decision, out var reason) is { } problem)
        {
            return PreOrderResults.Fail<PreOrderDto>(problem.Code, problem.Message, 400);
        }

        var (order, visibility) = await _c.LoadVisibleAsync(tenantId, request.Id, ct);
        if (order is null || _c.CallerId is not { } me)
        {
            return PreOrderResults.NotFound<PreOrderDto>();
        }

        if (order.Status != PreOrderStatuses.PendingApproval)
        {
            return order.Approval?.DecidedAt is not null && order.Status is not (PreOrderStatuses.Draft or PreOrderStatuses.Cancelled)
                ? PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.AlreadyDecided, "The order was already decided.", 409)
                : PreOrderResults.Transition<PreOrderDto>(order.Status, "decided");
        }

        if (request.Input!.ExpectedVersion is { } expectedVersion && expectedVersion != order.Version)
        {
            return PreOrderResults.Stale<PreOrderDto>();
        }

        if (order.Rep.UserId == me)
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.Sod, "The rep cannot decide on their own order (separation of duties).", 403);
        }

        if (!visibility.IsApprover(order))
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.NotApprover, "Only the resolved approver decides on this order.", 403);
        }

        var channel = PreOrderApprovalChannels.WorkCenter;
        if (PreOrderResults.Clean(request.Input.LinkToken) is { } token)
        {
            var (link, state) = await _approval.LinkStateAsync(tenantId, token, order, ct);
            if (link is null || link.PreOrderId != order.Id || !link.ApproverUserIds.Contains(me))
            {
                return PreOrderResults.NotFound<PreOrderDto>();
            }

            if (state != PreOrderApprovalLinkStates.Valid || !await _approval.ConsumeAsync(link, me, ct))
            {
                return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.LinkUnusable,
                    $"The approval link cannot be used ({(state == PreOrderApprovalLinkStates.Valid ? PreOrderApprovalLinkStates.Consumed : state)}).", 409);
            }

            channel = PreOrderApprovalChannels.EmailLink;
        }

        if (await _approval.DecideOnWorkflowAsync(order, decision, reason, ct) is { } refused)
        {
            return refused;
        }

        if (!await _approval.ApplyDecisionAsync(order, decision, me, reason, channel, _c.Now, ct))
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.AlreadyDecided, "The order was already decided.", 409);
        }

        var current = await _c.Orders.GetByIdAsync(tenantId, order.Id, ct) ?? order;
        return Response<PreOrderDto>.Success(_c.Dto(current, visibility));
    }

    // ── after the decision ───────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<PreOrderDto>> Handle(CancelPreOrderCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderDto>("Tenant context is required.");
        }

        if (PreOrderValidator.CheckReason(request.Input?.Reason, out var reason) is { } problem)
        {
            return PreOrderResults.Fail<PreOrderDto>(problem.Code, problem.Message, 400);
        }

        var (order, visibility) = await _c.LoadVisibleAsync(tenantId, request.Id, ct);
        if (order is null)
        {
            return PreOrderResults.NotFound<PreOrderDto>();
        }

        if (!visibility.IsOwner(order) && !visibility.IsManagerOf(order))
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.CancelForbidden, "Only the rep or the rep's manager cancels the order (D5).", 403);
        }

        if (!PreOrderStateMachine.CanTransition(order.Status, PreOrderStateMachine.Actions.Cancel))
        {
            return PreOrderResults.Transition<PreOrderDto>(order.Status, "cancelled");
        }

        if (request.Input!.ExpectedVersion is { } expectedVersion && expectedVersion != order.Version)
        {
            return PreOrderResults.Stale<PreOrderDto>();
        }

        var expected = order.Version;
        var wasTransmitted = order.Status == PreOrderStatuses.Transmitted;
        order.Cancellation = new PreOrderCancellation
        {
            ByUserId = _c.CallerId!.Value, At = _c.Now, Reason = reason!, NotifiedDepot = wasTransmitted, NotifiedPharmacy = wasTransmitted
        };
        PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.Cancel, PreOrderEventTypes.Cancelled, _c.CallerText, _c.Now,
            new Dictionary<string, string?> { ["reason"] = reason });
        if (!await _c.SaveAsync(order, expected, ct))
        {
            return PreOrderResults.Stale<PreOrderDto>();
        }

        await _notifier.CancelledAsync(order, reason!, ct);
        return Response<PreOrderDto>.Success(_c.Dto(order, visibility));
    }

    public async Task<Response<PreOrderDto>> Handle(CopyPreOrderCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderDto>("Tenant context is required.");
        }

        var (source, _) = await _c.LoadVisibleAsync(tenantId, request.Id, ct);
        if (source is null)
        {
            return PreOrderResults.NotFound<PreOrderDto>();
        }

        if (!PreOrderStateMachine.CanCopy(source.Status))
        {
            return PreOrderResults.Transition<PreOrderDto>(source.Status, "copied");
        }

        // D6: a NEW draft of the caller — new number; lines, pharmacy, depot and note copied; price, signature, approval not
        var input = new PreOrderInput(source.Pharmacy.AccountId, source.Depot?.AccountId, source.RequestedDeliveryDate is { } d && d >= _c.Today ? d : null,
            source.Note, source.VisitId,
            source.Lines.Select(l => new PreOrderLineInput(l.SkuId, l.Quantity, l.Mf is { } m ? new PreOrderMfInput(m.Buy, m.Free) : null, l.DiscountPct)).ToList());
        var (seller, sellerError) = await _c.SellerAsync<PreOrderDto>(tenantId, ct);
        if (sellerError is not null)
        {
            return sellerError;
        }

        var mode = await _pricing.ModeAsync(tenantId, seller!.LegalEntity, _c.Today,
            PreOrderContext.TierFor(await _c.SettingsAsync(tenantId, ct), seller.LegalEntity.CountryCode), ct);
        if (mode.Mode != PricingModes.Priced)
        {
            input = input with { Lines = input.Lines!.Select(l => l with { DiscountPct = null }).ToList() };
        }

        var created = await Handle(new CreatePreOrderCommand(input), ct);
        if (!created.IsSuccessful || created.Data is not { } dto)
        {
            return created;
        }

        var copy = (await _c.Orders.GetByIdAsync(tenantId, dto.Id, ct))!;
        var expected = copy.Version;
        copy.CopiedFromPreOrderId = source.Id;
        PreOrderStateMachine.Append(copy, PreOrderEventTypes.CopiedFrom, _c.CallerText, _c.Now, new Dictionary<string, string?> { ["source"] = source.Code });
        await _c.SaveAsync(copy, expected, ct);
        return Response<PreOrderDto>.Success(_c.Dto(copy, await _c.VisibilityAsync(tenantId, ct)), 201);
    }

    public async Task<Response<PreOrderDto>> Handle(TransmitPreOrderCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderDto>("Tenant context is required.");
        }

        var (order, visibility) = await _c.LoadVisibleAsync(tenantId, request.Id, ct);
        if (order is null)
        {
            return PreOrderResults.NotFound<PreOrderDto>();
        }

        if (order.Status != PreOrderStatuses.TransmissionFailed)
        {
            return PreOrderResults.Transition<PreOrderDto>(order.Status, "transmitted again");
        }

        if (request.Input?.ExpectedVersion is { } expectedVersion && expectedVersion != order.Version)
        {
            return PreOrderResults.Stale<PreOrderDto>();
        }

        await _transmission.TransmitAsync(order, _c.CallerText, ct);
        var current = await _c.Orders.GetByIdAsync(tenantId, order.Id, ct) ?? order;
        return Response<PreOrderDto>.Success(_c.Dto(current, visibility));
    }

    public async Task<Response<PreOrderDto>> Handle(RecordPreOrderDepotResponseCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderDto>("Tenant context is required.");
        }

        var (order, visibility) = await _c.LoadVisibleAsync(tenantId, request.Id, ct);
        if (order is null)
        {
            return PreOrderResults.NotFound<PreOrderDto>();
        }

        if (!PreOrderStateMachine.CanRecordDepotResponse(order.Status))
        {
            return PreOrderResults.Transition<PreOrderDto>(order.Status, "given a depot answer");
        }

        if (PreOrderValidator.CheckDepotResponse(request.Input, order, out var status) is { } problem)
        {
            return PreOrderResults.Fail<PreOrderDto>(problem.Code, problem.Message, 400);
        }

        if (request.Input!.ExpectedVersion is { } expectedVersion && expectedVersion != order.Version)
        {
            return PreOrderResults.Stale<PreOrderDto>();
        }

        var expected = order.Version;
        order.DepotResponse = new PreOrderDepotResponse
        {
            Status = status, Note = PreOrderResults.Clean(request.Input.Note), ByUserId = _c.CallerId ?? Guid.Empty, At = _c.Now,
            PartialLines = (request.Input.PartialLines ?? Array.Empty<PreOrderPartialLineInput>())
                .Select(p => new PreOrderPartialLine { LineNo = p.LineNo, DeliveredQuantity = p.DeliveredQuantity }).ToList()
        };
        PreOrderStateMachine.Append(order, PreOrderEventTypes.DepotResponseRecorded, _c.CallerText, _c.Now, new Dictionary<string, string?> { ["status"] = status });
        return await _c.SaveAsync(order, expected, ct) ? Response<PreOrderDto>.Success(_c.Dto(order, visibility)) : PreOrderResults.Stale<PreOrderDto>();
    }

    // ── settings ─────────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<PreOrderSettingsDto>> Handle(UpdatePreOrderSettingsCommand request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderSettingsDto>("Tenant context is required.");
        }

        if (PreOrderSettingsValidator.Check(request.Input) is { } problem)
        {
            return PreOrderResults.Fail<PreOrderSettingsDto>(problem.Code, problem.Message, 400);
        }

        var stored = await _c.Settings.GetAsync(tenantId, ct);
        var settings = stored ?? new PreOrderSettings { TenantId = tenantId, CreatedAt = _c.Now };
        if (request.Input.ExpectedVersion is { } expectedVersion && expectedVersion != settings.Version)
        {
            return PreOrderResults.Stale<PreOrderSettingsDto>();
        }

        if (request.Input.CompanyPrefixes is { } prefixes)
        {
            var next = prefixes.ToDictionary(p => p.LegalEntityId!.Value, p => p.Prefix!.Trim());
            foreach (var current in settings.CompanyPrefixes)
            {
                // RCS-001 §5.4: a prefix that numbered an order never changes (nor leaves its company)
                if ((!next.TryGetValue(current.LegalEntityId, out var replacement) || replacement != current.Prefix)
                    && await _sequences.ExistsAsync(tenantId, current.Prefix, ct))
                {
                    return PreOrderResults.Fail<PreOrderSettingsDto>(PreOrderErrors.PrefixLocked,
                        $"The prefix {current.Prefix} already numbered orders; it cannot be changed or removed.", 409);
                }
            }

            foreach (var (legalEntityId, prefix) in next)
            {
                if (settings.CompanyPrefixes.All(c => c.Prefix != prefix) && await _sequences.ExistsAsync(tenantId, prefix, ct))
                {
                    return PreOrderResults.Fail<PreOrderSettingsDto>(PreOrderErrors.PrefixLocked,
                        $"The prefix {prefix} already numbered another company's orders.", 409);
                }
            }

            settings.CompanyPrefixes = next.Select(p => new PreOrderCompanyPrefix { LegalEntityId = p.Key, Prefix = p.Value }).ToList();
        }

        if (request.Input.ApprovalMode is { } mode)
        {
            settings.ApprovalMode = mode.Trim().ToLowerInvariant();
        }

        settings.ApprovalLinkTtlHours = request.Input.ApprovalLinkTtlHours ?? settings.ApprovalLinkTtlHours;
        settings.TransmissionRetryMax = request.Input.TransmissionRetryMax ?? settings.TransmissionRetryMax;
        if (request.Input.PriceTierByCountry is { } tiers)
        {
            settings.PriceTierByCountry = tiers.Select(t => new PreOrderCountryTier { CountryCode = t.CountryCode!.Trim().ToUpperInvariant(), Tier = t.Tier!.Trim().ToLowerInvariant() })
                .ToList();
        }

        settings.UpdatedAt = _c.Now;
        settings.UpdatedBy = _c.Actor.ActorName;
        return await _c.Settings.SaveAsync(settings, stored is null ? null : stored.Version, ct)
            ? Response<PreOrderSettingsDto>.Success(PreOrderMapping.ToDto(settings, false))
            : PreOrderResults.Stale<PreOrderSettingsDto>();
    }

    /// <summary>The caller's own draft (404 when not seen, 403 when seen but not theirs, 409 when no longer a draft or stale).</summary>
    private async Task<(PreOrder? Order, PreOrderVisibility? Visibility, Response<PreOrderDto>? Error)> OwnDraftAsync(Guid id, int? expectedVersion,
        CancellationToken ct, string action = PreOrderStateMachine.Actions.Edit)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return (null, null, PreOrderResults.Invalid<PreOrderDto>("Tenant context is required."));
        }

        var (order, visibility) = await _c.LoadVisibleAsync(tenantId, id, ct);
        if (order is null)
        {
            return (null, null, PreOrderResults.NotFound<PreOrderDto>());
        }

        if (!visibility.IsOwner(order))
        {
            return (null, null, PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.NotOwner, "Only the rep who opened the order changes it.", 403));
        }

        if (!PreOrderStateMachine.IsEditable(order.Status))
        {
            return (null, null, order.Status == PreOrderStatuses.PendingApproval || action == PreOrderStateMachine.Actions.Edit
                ? PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.NotEditable, $"A {order.Status} pre-order cannot be changed.", 409)
                : PreOrderResults.Transition<PreOrderDto>(order.Status, "submitted"));
        }

        if (expectedVersion is { } expected && expected != order.Version)
        {
            return (null, null, PreOrderResults.Stale<PreOrderDto>());
        }

        return (order, visibility, null);
    }
}
