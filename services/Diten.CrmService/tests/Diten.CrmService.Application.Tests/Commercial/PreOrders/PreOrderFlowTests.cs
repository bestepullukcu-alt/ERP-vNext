using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Queries;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Tests.Commercial.Pricing;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 §12 / §13 / §16 on the real pipeline: the draft (seller from the seat, number at first save,
/// field rules), the submission gates (lines, depot, signature, hard limit — server side), the price fixed at submission
/// (D12) with the MOD-0156 result, the approval rule (D1), the decision (first wins, SoD, request changes clears the
/// signature and keeps the log), the system transmission, cancel / copy / depot answer.
/// </summary>
public sealed class PreOrderFlowTests : PreOrderTestBase
{
    // ── the draft ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task F1_a_draft_takes_its_seller_country_currency_and_mode_from_the_system_and_its_number_on_first_save()
    {
        var first = await Draft();
        var second = await Draft();

        Assert.Equal(("MG-ORD-000001", "MG-ORD-000002"), (first.Code, second.Code));
        Assert.Equal((PreOrderWorld.LeTr, "MG", "GM-TR", "TR", "TRY"), (first.Seller.LegalEntityId, first.Seller.Prefix, first.Seller.Code, first.CountryCode,
            first.LocalCurrencyCode));
        Assert.Equal((PreOrderPriceModes.Priced, "TRY", PreOrderStatuses.Draft), (first.PriceMode, first.Currency, first.Status));
        Assert.Equal((PreOrderWorld.Rep, "Ayşe Temsilci", "TR-MEDICAL-REPRESENTATIVE"), (first.Rep.UserId, first.Rep.DisplayName, first.Rep.PositionCode));
        var line = Assert.Single(first.Lines);
        Assert.Equal(("LS-TUT", 28m, "Lsku", 1), (line.Sku.Code, line.Sku.PackQuantity, line.SkuLevel, line.LineNo));
        Assert.Equal(("Eczane Şifa", "siparis@selcuk.test"), (first.Pharmacy.Name, first.Depot!.Email));
        Assert.Equal(PreOrderEventTypes.Created, Assert.Single(first.Events).Type);
    }

    [Fact]
    public async Task F2_no_seat_is_422_an_unreachable_seat_or_MDM_is_503_and_a_company_without_a_prefix_is_422_with_nothing_written()
    {
        W.Seats.Remove(PreOrderWorld.Rep);
        var noSeat = await W.Send(new CreatePreOrderCommand(Input()), PreOrderWorld.Rep);
        W.Seats.Add(PreOrderWorld.Rep, PreOrderWorld.LeTr);
        W.Seats.Down = true;
        var seatDown = await W.Send(new CreatePreOrderCommand(Input()));
        W.Seats.Down = false;
        W.Mdm.Down = true;
        var mdmDown = await W.Send(new CreatePreOrderCommand(Input()));
        W.Mdm.Down = false;
        W.Settings.Store.Single().CompanyPrefixes.RemoveAll(p => p.LegalEntityId == PreOrderWorld.LeTr);
        var noPrefix = await W.Send(new CreatePreOrderCommand(Input()));

        Assert.Equal((PreOrderErrors.NoSeat, 422), (Code(noSeat), noSeat.StatusCode));
        Assert.Equal((PreOrderErrors.ContextUnavailable, 503), (Code(seatDown), seatDown.StatusCode));
        Assert.Equal((PreOrderErrors.ContextUnavailable, 503), (Code(mdmDown), mdmDown.StatusCode));
        Assert.Equal((PreOrderErrors.NoCompanyPrefix, 422), (Code(noPrefix), noPrefix.StatusCode));
        Assert.Empty(W.Orders.Store);
    }

    public static TheoryData<string, string> BadDrafts => new()
    {
        { "retired", PreOrderErrors.SkuRetired },
        { "unknown", PreOrderErrors.SkuNotFound },
        { "twice", PreOrderErrors.LinesInvalid },
        { "zero-quantity", PreOrderErrors.LinesInvalid },
        { "mf", PreOrderErrors.MfInvalid },
        { "discount", PreOrderErrors.DiscountInvalid },
        { "discount-decimals", PreOrderErrors.DiscountInvalid },
        { "past-date", PreOrderErrors.DeliveryDateInvalid },
        { "pharmacy-is-a-depot", PreOrderErrors.PharmacyInvalid },
        { "foreign-pharmacy", PreOrderErrors.PharmacyInvalid },
        { "depot-is-a-pharmacy", PreOrderErrors.DepotInvalid },
        { "note", PreOrderErrors.NoteTooLong }
    };

    [Theory]
    [MemberData(nameof(BadDrafts))]
    public async Task F3_a_bad_draft_is_400_and_nothing_is_written(string what, string code)
    {
        var input = what switch
        {
            "retired" => Input(lines: Line("AL5")),
            "unknown" => Input(lines: new PreOrderLineInput(Guid.NewGuid(), 1)),
            "twice" => Input(lines: [Line("TUT"), Line("TUT", 5)]),
            "zero-quantity" => Input(lines: Line("TUT", 0)),
            "mf" => Input(lines: Line("TUT", 20, (0, 2))),
            "discount" => Input(lines: Line("TUT", 20, null, 100m)),
            "discount-decimals" => Input(lines: Line("TUT", 20, null, 5.25m)),
            "past-date" => Input() with { RequestedDeliveryDate = W.Today.AddDays(-1) },
            "pharmacy-is-a-depot" => Input(pharmacy: PreOrderWorld.Depot),
            "foreign-pharmacy" => Input(pharmacy: PreOrderWorld.ForeignPharmacy),
            "depot-is-a-pharmacy" => Input(depot: PreOrderWorld.Pharmacy2),
            _ => Input() with { Note = new string('n', 1001) }
        };

        var response = await W.Send(new CreatePreOrderCommand(input), PreOrderWorld.Rep);

        Assert.Equal((code, 400), (Code(response), response.StatusCode));
        Assert.Empty(W.Orders.Store);
    }

    [Fact]
    public async Task F3b_with_no_list_in_force_the_order_is_in_quantity_mode_and_a_discount_is_refused()
    {
        var withDiscount = await W.Send(new CreatePreOrderCommand(Input(lines: Line("GLU", 30, null, 3m))), PreOrderWorld.RepKg);
        var draft = Ok(await W.Send(new CreatePreOrderCommand(Input(lines: Line("GLU", 30, (20, 3))))));

        Assert.Equal((PreOrderErrors.DiscountNotAllowed, 400), (Code(withDiscount), withDiscount.StatusCode));
        Assert.Equal(("GMKG-ORD-000001", PreOrderPriceModes.Quantity, (string?)null), (draft.Code, draft.PriceMode, draft.Currency));
    }

    [Fact]
    public async Task F4_a_discarded_draft_is_cancelled_and_its_number_is_never_given_again()
    {
        var draft = await Draft();
        var discarded = Ok(await W.Send(new DiscardPreOrderCommand(draft.Id, new PreOrderVersionInput(draft.Version))));
        var next = await Draft();

        Assert.Equal((PreOrderStatuses.Cancelled, "MG-ORD-000002"), (discarded.Status, next.Code));
        Assert.Contains(discarded.Events, e => e.Type == PreOrderEventTypes.Discarded);
    }

    [Fact]
    public async Task F4b_a_stale_version_is_409_and_another_rep_cannot_edit_my_draft()
    {
        var draft = await Draft();
        var stale = await W.Send(new UpdatePreOrderCommand(draft.Id, Input() with { ExpectedVersion = draft.Version - 1 }));
        W.Team.Reps[PreOrderWorld.OtherRep] = [PreOrderWorld.Rep.ToString("D")]; // even a rep who sees it
        var other = await W.Send(new UpdatePreOrderCommand(draft.Id, Input()), PreOrderWorld.OtherRep);

        Assert.Equal((PreOrderErrors.Stale, 409), (Code(stale), stale.StatusCode));
        Assert.Equal((PreOrderErrors.NotOwner, 403), (Code(other), other.StatusCode));
    }

    // ── the submission ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task F5_the_server_refuses_a_submission_without_lines_depot_signature_or_depot_e_mail_and_with_a_hard_limit()
    {
        var noLines = await Draft(input: Input() with { Lines = [] });
        var noDepot = await Draft(input: Input() with { DepotAccountId = null });
        var unsigned = await Draft();
        var depotNoEmail = await Draft(input: Input(depot: PreOrderWorld.DepotNoEmail));
        var hard = await Draft(input: Input(lines: Line("TUT", 20, null, 16m)));   // 16 % > the hard 15 %
        foreach (var d in new[] { noLines, noDepot, depotNoEmail, hard })
        {
            await Sign(d.Id);
        }

        var r1 = await Submit(noLines.Id);
        var r2 = await Submit(noDepot.Id);
        var r3 = await Submit(unsigned.Id);
        var r4 = await Submit(depotNoEmail.Id);
        var r5 = await Submit(hard.Id);

        Assert.Equal((PreOrderErrors.LinesInvalid, 400), (Code(r1), r1.StatusCode));
        Assert.Equal((PreOrderErrors.DepotRequired, 400), (Code(r2), r2.StatusCode));
        Assert.Equal((PreOrderErrors.SignatureRequired, 422), (Code(r3), r3.StatusCode));
        Assert.Equal((PreOrderErrors.DepotNoEmail, 422), (Code(r4), r4.StatusCode));
        Assert.Equal((PreOrderErrors.HardLimit, 422), (Code(r5), r5.StatusCode));
        Assert.Contains("L1:discount.hard-exceeded", r5.Errors!);
        Assert.All(W.Orders.Store, o => Assert.Equal((PreOrderStatuses.Draft, 0), (o.Status, o.SubmissionNo)));
        Assert.Empty(W.Workflow.Starts);
    }

    [Fact]
    public async Task F5b_a_SKU_retired_after_the_draft_is_refused_at_submission()
    {
        var draft = await Draft();
        await Sign(draft.Id);
        W.Mdm.Skus[PricingWorld.Sku("TUT")] = W.Mdm.Skus[PricingWorld.Sku("TUT")] with { LifecycleStatus = "Retired" };

        var submitted = await Submit(draft.Id);

        Assert.Equal((PreOrderErrors.SkuRetired, 400), (Code(submitted), submitted.StatusCode));
        Assert.Equal(PreOrderStatuses.Draft, W.Stored(draft.Id).Status);
    }

    [Fact]
    public async Task F6_always_mode_sends_every_order_to_the_rep_s_line_managers_with_the_MOD_0156_result_fixed_on_it()
    {
        var pending = await Pending();

        Assert.Equal(1, pending.SubmissionNo);
        var line = Assert.Single(pending.Lines);
        // MF 10+2 (16.7 %) with 5 % → combined 1 − 0.95 × 0.833 = 20.8 % > the soft 20 % (MOD-0156's numbers, copied)
        Assert.Equal((412.60m, 10m, 4, 16.7m, 20.8m, PricingOutcomesSoft), (line.UnitPrice, line.VatRate, line.FreeQuantity, line.MfRatioPct,
            line.CombinedDiscountPct, line.Outcome));
        Assert.Equal((PriceTiers.PharmacistPurchase, "TR Resmî"), (pending.Price!.Tier, pending.Price.Name));
        Assert.Equal(pending.Evaluation!.Outcome, pending.Lines.Single().Outcome);
        Assert.Null(pending.Fx);                                                     // TRY list, TRY local: no equivalent
        Assert.Equal(new[] { PreOrderApprovalReasons.TenantSetting, PreOrderApprovalReasons.SoftExceeded }, pending.ApprovalRequirement!.Reasons);
        Assert.Equal(new[] { PreOrderWorld.Manager, PreOrderWorld.Manager2 }, pending.ApprovalRequirement.ApproverUserIds);
        Assert.Equal("LineManager", pending.ApprovalRequirement.FallbackLevel);

        var start = Assert.Single(W.Workflow.Starts);
        Assert.Equal(("COM-PREORDER-APPROVAL", "commercial.pre-order", pending.Id.ToString("D")), (start.TemplateCode, start.ObjectType, start.ObjectId));
        Assert.Equal(new[] { PreOrderWorld.Manager.ToString("D"), PreOrderWorld.Manager2.ToString("D") }, start.CandidatePrincipalIds);
        Assert.Equal($"/Commercial/PreOrders/Approve/{pending.Id:D}", start.DisplayContext.DeepLinkUrl);
        Assert.Equal(new[] { PreOrderEventTypes.Created, PreOrderEventTypes.SignatureCaptured, PreOrderEventTypes.Submitted, PreOrderEventTypes.ApprovalRequested },
            pending.Events.Select(e => e.Type));
        Assert.Equal("approval-requested", Assert.Single(W.Notifier.Events).Event);
    }

    private const string PricingOutcomesSoft = "soft";

    [Fact]
    public async Task F7_auto_within_limits_approves_and_transmits_an_order_without_findings_and_a_soft_finding_still_needs_the_manager()
    {
        await SetMode(PreOrderApprovalModes.AutoWithinLimits);
        var clean = await Draft(input: Input(lines: Line("TUT", 20, (10, 2))));
        await Sign(clean.Id);
        var soft = await Draft(input: Input(lines: Line("TUT", 20, null, 11m)));   // 11 % > the soft 10 %, < the hard 15 %
        await Sign(soft.Id);

        var auto = Ok(await Submit(clean.Id));
        var manual = Ok(await Submit(soft.Id));

        Assert.Equal((PreOrderStatuses.Transmitted, true, PreOrderApprovalChannels.Auto), (auto.Status, auto.Approval!.Auto, auto.Approval.Channel));
        Assert.False(auto.ApprovalRequirement!.Required);
        Assert.Contains(auto.Events, e => e.Type == PreOrderEventTypes.AutoApproved);
        Assert.Equal((PreOrderStatuses.PendingApproval, PreOrderApprovalReasons.SoftExceeded), (manual.Status, Assert.Single(manual.ApprovalRequirement!.Reasons)));
        Assert.Single(W.Workflow.Starts);                                             // only the soft one asked MOD-0023
        Assert.Contains(W.Notifier.Events, e => e.Event == "auto-approved" && e.OrderId == clean.Id);
    }

    [Fact]
    public async Task F8_a_pending_order_is_never_edited_signed_discarded_or_submitted_again()
    {
        var pending = await Pending();

        var update = await W.Send(new UpdatePreOrderCommand(pending.Id, Input()));
        var sign = await W.Send(new CapturePreOrderSignatureCommand(pending.Id, new PreOrderSignatureInput("X", Png, "image/png")));
        var discard = await W.Send(new DiscardPreOrderCommand(pending.Id, new PreOrderVersionInput()));
        var again = await Submit(pending.Id);

        Assert.All(new[] { update, sign, discard, again }, r => Assert.Equal((PreOrderErrors.NotEditable, 409), (Code(r), r.StatusCode)));
        Assert.Equal(pending.Version, W.Stored(pending.Id).Version);
    }

    // ── the decision ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task F9_the_rep_never_decides_their_own_order_and_a_stranger_does_not_even_see_it()
    {
        var pending = await Pending();

        var own = await Decide(pending.Id, PreOrderDecisions.Approve, by: PreOrderWorld.Rep);           // a rep who also holds approve
        var stranger = await Decide(pending.Id, PreOrderDecisions.Approve, by: PreOrderWorld.Stranger);

        Assert.Equal((PreOrderErrors.Sod, 403), (Code(own), own.StatusCode));
        Assert.Equal(404, stranger.StatusCode);
        Assert.Empty(W.Decisions.Calls);
        Assert.Equal(PreOrderStatuses.PendingApproval, W.Stored(pending.Id).Status);
    }

    [Fact]
    public async Task F10_an_approval_moves_to_approved_then_the_system_transmits_and_the_second_approver_finds_it_decided()
    {
        var pending = await Pending();

        var approved = Ok(await Decide(pending.Id, PreOrderDecisions.Approve));
        var second = await Decide(pending.Id, PreOrderDecisions.Reject, "geç", PreOrderWorld.Manager2);

        Assert.Equal((PreOrderStatuses.Transmitted, PreOrderWorld.Manager, PreOrderApprovalChannels.WorkCenter),
            (approved.Status, approved.Approval!.DecidedByUserId!.Value, approved.Approval.Channel));
        Assert.Equal(PreOrderTransmissionStatuses.Sent, approved.Transmission!.LastStatus);
        Assert.Equal((PreOrderErrors.AlreadyDecided, 409), (Code(second), second.StatusCode));
        Assert.Single(W.Decisions.Calls);
        Assert.Equal(new[] { PreOrderEventTypes.Approved, PreOrderEventTypes.TransmissionAttempted, PreOrderEventTypes.Transmitted },
            approved.Events.Skip(4).Select(e => e.Type));
    }

    [Fact]
    public async Task F11_a_rejection_needs_its_reason_ends_the_order_and_the_copy_is_a_new_draft_without_price_signature_or_approval()
    {
        var pending = await Pending();
        var bare = await Decide(pending.Id, PreOrderDecisions.Reject, " ");
        var rejected = Ok(await Decide(pending.Id, PreOrderDecisions.Reject, "Stok fazlası"));
        var copy = Ok(await W.Send(new CopyPreOrderCommand(pending.Id), PreOrderWorld.Rep));

        Assert.Equal((PreOrderErrors.ReasonRequired, 400), (Code(bare), bare.StatusCode));
        Assert.Equal((PreOrderStatuses.Rejected, "Stok fazlası"), (rejected.Status, rejected.Approval!.Reason));
        Assert.Contains(W.Notifier.Events, e => e.Event == "rejected" && e.Detail == "Stok fazlası");
        Assert.Equal(("MG-ORD-000002", PreOrderStatuses.Draft, (Guid?)pending.Id), (copy.Code, copy.Status, copy.CopiedFromPreOrderId));
        Assert.Null(copy.Signature);
        Assert.Null(copy.Approval);
        Assert.Null(copy.Price);
        Assert.Equal(pending.Lines.Select(l => (l.SkuId, l.Quantity, l.DiscountPct)), copy.Lines.Select(l => (l.SkuId, l.Quantity, l.DiscountPct)));
        Assert.Contains(copy.Events, e => e.Type == PreOrderEventTypes.CopiedFrom);
        Assert.Equal(409, (await W.Send(new CopyPreOrderCommand((await Draft()).Id), PreOrderWorld.Rep)).StatusCode);   // a draft is not copied
    }

    [Fact]
    public async Task F12_request_changes_sends_it_back_to_draft_clears_the_signature_and_keeps_every_event()
    {
        var pending = await Pending();
        var eventsBefore = W.Stored(pending.Id).Events.Count;

        var back = Ok(await Decide(pending.Id, PreOrderDecisions.RequestChanges, "MF'yi 10+2 yapın"));

        Assert.Equal(PreOrderStatuses.Draft, back.Status);
        Assert.Null(back.Signature);
        Assert.True(back.Events.Count > eventsBefore);
        Assert.Equal(new[] { PreOrderEventTypes.ChangesRequested, PreOrderEventTypes.SignatureCleared }, back.Events.Skip(eventsBefore).Select(e => e.Type));
        var resubmit = await Submit(pending.Id);
        Assert.Equal((PreOrderErrors.SignatureRequired, 422), (Code(resubmit), resubmit.StatusCode));
        await Sign(pending.Id);
        var second = Ok(await Submit(pending.Id));
        Assert.Equal((2, PreOrderStatuses.PendingApproval), (second.SubmissionNo, second.Status));
        Assert.Equal(2, W.Workflow.Starts.Count);
    }

    // ── after the decision ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task F13_a_failed_transmission_keeps_the_approval_and_sales_operations_retries_it()
    {
        W.Transmitter.Next.Enqueue(new PreOrderTransmissionResult(PreOrderTransmissionResult.Failed, [], null, null, "platform_unavailable"));
        var failed = await Approved();
        Assert.Equal((PreOrderStatuses.TransmissionFailed, PreOrderDecisions.Approve), (failed.Status, failed.Approval!.Decision));

        var notFailed = await W.Send(new TransmitPreOrderCommand((await Draft()).Id, new PreOrderVersionInput()), PreOrderWorld.Ops);
        var retried = Ok(await W.Send(new TransmitPreOrderCommand(failed.Id, new PreOrderVersionInput()), PreOrderWorld.Ops));

        Assert.Equal(409, notFailed.StatusCode);
        Assert.Equal((PreOrderStatuses.Transmitted, 2), (retried.Status, retried.Transmission!.Attempts.Count));
        Assert.Equal(new[] { PreOrderTransmissionStatuses.Failed, PreOrderTransmissionStatuses.Sent }, retried.Transmission.Attempts.Select(a => a.Status));
    }

    [Fact]
    public async Task F13b_with_no_transmitter_wired_the_approved_order_stays_approved_never_a_fake_transmitted()
    {
        W.Transmitter.Next.Enqueue(PreOrderTransmissionResult.NotWired);
        var approved = await Approved();

        Assert.Equal(PreOrderStatuses.Approved, approved.Status);
        Assert.Null(approved.Transmission);
    }

    [Fact]
    public async Task F14_cancel_needs_a_reason_and_is_for_the_rep_and_the_rep_s_manager_only()
    {
        var mine = await Approved();
        var transmitted = await Approved();
        var rejected = await Pending();
        await Decide(rejected.Id, PreOrderDecisions.Reject, "hayır");

        var bare = await W.Send(new CancelPreOrderCommand(mine.Id, new PreOrderCancelInput(" ")), PreOrderWorld.Rep);
        var byOtherRep = await W.Send(new CancelPreOrderCommand(mine.Id, new PreOrderCancelInput("x")), PreOrderWorld.OtherRep);
        var byRep = Ok(await W.Send(new CancelPreOrderCommand(mine.Id, new PreOrderCancelInput("Eczane vazgeçti")), PreOrderWorld.Rep));
        var byManager = Ok(await W.Send(new CancelPreOrderCommand(transmitted.Id, new PreOrderCancelInput("Depo stoksuz")), PreOrderWorld.Manager));
        var rejectedCancel = await W.Send(new CancelPreOrderCommand(rejected.Id, new PreOrderCancelInput("x")), PreOrderWorld.Rep);

        Assert.Equal((PreOrderErrors.ReasonRequired, 400), (Code(bare), bare.StatusCode));
        Assert.Equal(404, byOtherRep.StatusCode);
        Assert.Equal((PreOrderStatuses.Cancelled, "Eczane vazgeçti", true), (byRep.Status, byRep.Cancellation!.Reason, byRep.Cancellation.NotifiedDepot));
        Assert.Equal((PreOrderStatuses.Cancelled, PreOrderWorld.Manager), (byManager.Status, byManager.Cancellation!.ByUserId));
        Assert.Equal((PreOrderErrors.InvalidTransition, 409), (Code(rejectedCancel), rejectedCancel.StatusCode));
        Assert.Equal(2, W.Notifier.Events.Count(e => e.Event == "cancelled"));
    }

    [Fact]
    public async Task F15_the_depot_answer_lands_on_a_transmitted_order_with_valid_partial_lines()
    {
        var pending = await Pending();
        var transmitted = await Approved();

        var early = await W.Send(new RecordPreOrderDepotResponseCommand(pending.Id, new PreOrderDepotResponseInput("delivered", null, null)), PreOrderWorld.Ops);
        var tooMany = await W.Send(new RecordPreOrderDepotResponseCommand(transmitted.Id,
            new PreOrderDepotResponseInput("partial", [new PreOrderPartialLineInput(1, 20)], null)), PreOrderWorld.Ops);
        var unknownLine = await W.Send(new RecordPreOrderDepotResponseCommand(transmitted.Id,
            new PreOrderDepotResponseInput("partial", [new PreOrderPartialLineInput(9, 1)], null)), PreOrderWorld.Ops);
        var partial = Ok(await W.Send(new RecordPreOrderDepotResponseCommand(transmitted.Id,
            new PreOrderDepotResponseInput("partial", [new PreOrderPartialLineInput(1, 12)], "8 kutu sonra")), PreOrderWorld.Ops));

        Assert.Equal(409, early.StatusCode);
        Assert.All(new[] { tooMany, unknownLine }, r => Assert.Equal((PreOrderErrors.DepotResponseInvalid, 400), (Code(r), r.StatusCode)));
        Assert.Equal((PreOrderStatuses.Transmitted, "partial", 12, PreOrderWorld.Ops),
            (partial.Status, partial.DepotResponse!.Status, partial.DepotResponse.PartialLines.Single().DeliveredQuantity, partial.DepotResponse.ByUserId));
    }

    // ── prices, rates and approvers ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task F16_the_price_is_fixed_at_submission_a_later_list_does_not_change_the_order_but_a_later_submission_takes_it()
    {
        var early = await Pending();
        var late = await Draft();
        await Sign(late.Id);
        var tut = W.PriceItems.Store.Single(i => i.SkuId == PricingWorld.Sku("TUT") && W.PriceLists.Store.Single(l => l.Id == i.PriceListId).CountryCode == "TR");
        tut.PharmacistPurchasePrice = 430.00m;                                       // the list in force changes after the first submission

        var lateSubmitted = Ok(await Submit(late.Id));
        var earlyNow = Ok(await W.Send(new GetPreOrderByIdQuery(early.Id), PreOrderWorld.Rep));

        Assert.Equal(412.60m, earlyNow.Lines.Single().UnitPrice);
        Assert.Equal(early.Totals!.Gross, earlyNow.Totals!.Gross);
        Assert.Equal(430.00m, lateSubmitted.Lines.Single().UnitPrice);
    }

    [Fact]
    public async Task F17_a_USD_list_in_an_AED_company_gets_the_rate_snapshot_and_a_quantity_order_has_no_amounts()
    {
        var ae = await Pending(PreOrderWorld.RepAe, Input(lines: Line("TUT", 20, (10, 2))));
        var kg = await Pending(PreOrderWorld.RepKg, Input(lines: Line("GLU", 30, (20, 3))));

        Assert.Equal(("USD", "AED"), (ae.Currency, ae.LocalCurrencyCode));
        Assert.Equal((3.6725m, W.Today.AddDays(-2), "Merkez Bankası", "AED"), (ae.Fx!.Rate, ae.Fx.RateDate, ae.Fx.Source, ae.Fx.LocalCurrency));
        Assert.Equal(ae.Totals!.Gross * 3.6725m, ae.Fx.LocalGross);
        Assert.Equal((PreOrderPriceModes.Quantity, (string?)null, (PreOrderTotals?)null, (PreOrderFxSnapshot?)null), (kg.PriceMode, kg.Currency, kg.Totals, kg.Fx));
        Assert.Null(kg.Lines.Single().UnitPrice);
        Assert.Equal(3, kg.Lines.Single().FreeQuantity);                            // MF still from MOD-0156 in quantity mode
    }

    [Fact]
    public async Task F18_Platform_down_at_submission_never_refuses_the_order_it_waits_and_the_approvers_are_resolved_on_read()
    {
        var draft = await Draft();
        await Sign(draft.Id);
        W.Managers.Down = true;

        var waiting = Ok(await Submit(draft.Id));
        Assert.Equal((PreOrderStatuses.PendingApproval, "unresolved"), (waiting.Status, waiting.ApprovalRequirement!.FallbackLevel));
        Assert.Empty(W.Workflow.Starts);
        Assert.Contains(waiting.Events, e => e.Type == PreOrderEventTypes.ApproverUnresolved);

        W.Managers.Down = false;
        var resolved = Ok(await W.Send(new GetPreOrderByIdQuery(draft.Id), PreOrderWorld.Rep));

        Assert.Equal((PreOrderStatuses.PendingApproval, "LineManager"), (resolved.Status, resolved.ApprovalRequirement!.FallbackLevel));
        Assert.Single(W.Workflow.Starts);
        Assert.NotNull(resolved.Approval!.WorkflowInstanceId);
        Assert.Single(W.Links.Store);
    }

    [Fact]
    public async Task F19_no_approver_above_the_seat_is_422_and_a_missing_workflow_template_is_409_with_nothing_written()
    {
        var draft = await Draft();
        await Sign(draft.Id);
        W.Managers.Set(PreOrderWorld.Rep);
        var noApprover = await Submit(draft.Id);
        W.Managers.Set(PreOrderWorld.Rep, PreOrderWorld.Manager);
        W.Workflow.StartOutcome = ClaimWorkflowCallOutcome.TemplateMissing;
        var noTemplate = await Submit(draft.Id);

        Assert.Equal((PreOrderErrors.NoApprover, 422), (Code(noApprover), noApprover.StatusCode));
        Assert.Equal((PreOrderErrors.WorkflowTemplateMissing, 409), (Code(noTemplate), noTemplate.StatusCode));
        Assert.Equal((PreOrderStatuses.Draft, 0), (W.Stored(draft.Id).Status, W.Stored(draft.Id).SubmissionNo));
        Assert.Empty(W.Links.Store);
    }

    [Fact]
    public async Task F20_the_preview_evaluates_like_the_submission_and_writes_nothing()
    {
        var preview = Ok(await W.Send(new PreviewPreOrderQuery(new PreOrderPreviewInput(PreOrderWorld.Pharmacy, [Line("TUT", 20, null, 11m)])),
            PreOrderWorld.Rep));

        Assert.Equal(("priced", "soft", true), (preview.Mode, preview.Outcome, preview.ApprovalRequired));
        var finding = Assert.Single(preview.Lines.Single().Findings);
        Assert.Equal(("discount.soft-exceeded", "soft"), (finding.Code, finding.Severity));
        Assert.Equal("10", finding.Params["limit"]);                               // a parameter, never a sentence
        Assert.Empty(W.Orders.Store);
        Assert.Empty(W.Audit.Bodies);
    }

    [Fact]
    public async Task F21_the_context_strip_comes_from_the_seat_MDM_and_MOD_0156_with_the_approver_preview()
    {
        var context = Ok(await W.Send(new GetPreOrderContextQuery(), PreOrderWorld.Rep));

        Assert.Equal((PreOrderWorld.LeTr, "MG", "TR", "TRY", "priced", "TRY", PriceTiers.PharmacistPurchase, "always"),
            (context.LegalEntityId, context.CompanyPrefix, context.CountryCode, context.LocalCurrencyCode, context.PriceMode, context.Currency, context.Tier,
                context.ApprovalMode));
        Assert.Equal(new[] { PreOrderWorld.Manager, PreOrderWorld.Manager2 }, context.Approver.Managers.Select(m => m.UserId));
    }
}
