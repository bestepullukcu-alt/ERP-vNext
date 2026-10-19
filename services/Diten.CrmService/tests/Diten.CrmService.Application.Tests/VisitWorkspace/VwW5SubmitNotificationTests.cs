using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Application.Features.VisitReport.V2;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W5 · W5-BE (K-N4 / K-N5 / K-N8) on the PRODUCTION V2 submit handler + notifier + sender: a submit queues ONE
/// report summary for the rep's line manager; a second submit / an amendment sends nothing new; an adverse event goes to
/// the PV mailbox with the manager in cc (route-pending to the manager alone without it); an off-label request to the MSL
/// mailbox the same way; a pharmacy report has no off-label e-mail; a Platform that is down or throws never fails the
/// report (the row is "failed" and retried); the variables carry no patient data.
/// </summary>
public sealed partial class VwW4ReportV2Tests
{
    private static W5Notifications Wire(World w)
    {
        var n = new W5Notifications(w.Clock.GetUtcNow());
        w.Notifier = n.Notifier(w.Contacts, w.Accounts, w.Reports, w.Plans);
        return n;
    }

    [Fact]
    public async Task W5_a_submit_queues_one_summary_for_the_line_manager_and_a_second_submit_or_an_amendment_sends_nothing_new()
    {
        var w = new World();
        var n = Wire(w);
        var plan = w.DoctorPlan();
        plan.Resource.DisplayName = "Okan Tekin";
        var body = w.Complete(plan);
        body.General!.GeneralNote = "Literatür istendi.";

        var res = await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default);
        Assert.Equal(201, res.StatusCode);
        var row = Assert.Single(n.Rows.Items);
        Assert.Equal((VisitNotificationEvents.ReportSubmitted, VisitNotificationStatuses.Sent, res.Data!.VisitReportId!.Value, 1),
            (row.EventCode, row.Status, row.SubjectId, row.Attempts));
        var request = Assert.Single(n.Platform.Requests);
        Assert.Equal((Tenant, VisitNotificationEvents.ReportSubmitted, "MOD-0155"), (request.TenantId, request.EventCode, request.SourceModule));
        Assert.Equal(Rep, Assert.Single(request.To).LineManagerOf);              // the Platform resolves the manager (K-N2)
        Assert.Empty(request.Cc);
        Assert.Equal("Okan Tekin", request.Variables["repName"]);
        Assert.StartsWith("Doctor ", request.Variables["doctorName"]);
        Assert.Equal(plan.PlannedDate.ToString("yyyy-MM-dd"), request.Variables["visitDate"]);
        Assert.Equal("Literatür istendi.", request.Variables["note"]);
        Assert.Equal("https://crm.test/CRM/VisitReport?plannedVisitId=" + plan.Id.ToString("D"), request.Variables["reportUrl"]);
        Assert.Equal(("false", "false"), (request.Variables["adverseEvent"], request.Variables["offLabelRequest"]));
        Assert.DoesNotContain(request.Variables.Keys, k => k.Contains("patient", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("crm:" + VisitNotificationEvents.ReportSubmitted + ":" + row.SubjectId.ToString("D"), request.CorrelationId);
        Assert.Contains(row.Resolved, r => r.Via == "line-manager");
        Assert.DoesNotContain("@", string.Join("|", row.Resolved.Select(r => r.UserId)));   // no address on the row

        // the same report again inside the correction window: no new e-mail
        Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, w.Complete(plan), null), default)).IsSuccessful);
        Assert.Single(n.Rows.Items);
        Assert.Single(n.Platform.Requests);

        // an amendment (even one that adds an adverse event) sends nothing (K-N4)
        var corrected = w.Complete(plan);
        corrected.Safety!.AdverseEvent = true;
        corrected.Safety.AdverseEventNote = "Baş dönmesi";
        Assert.True((await w.Handlers().Handle(new AmendVisitReportV2Command(row.SubjectId, "Advers olay sonradan aktarıldı", corrected, null), default)).IsSuccessful);
        Assert.Single(n.Platform.Requests);
    }

    [Fact]
    public async Task W5_an_adverse_event_goes_to_the_pv_mailbox_with_the_manager_in_cc_and_without_it_to_the_manager_alone_as_route_pending()
    {
        var w = new World();
        var n = Wire(w);
        n.Settings.Items.Add(new VisitNotificationSettings { TenantId = Tenant, PvEmail = "pv@grandmedical.test", ReportSummary = false });
        var plan = w.DoctorPlan();
        var body = w.Complete(plan);
        body.Safety!.AdverseEvent = true;
        body.Safety.AdverseEventNote = "Hastada baş dönmesi";

        Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).IsSuccessful);
        var request = Assert.Single(n.Platform.Requests);                         // the summary switched off; the safety mail always
        Assert.Equal(VisitNotificationEvents.AdverseEvent, request.EventCode);
        Assert.Equal("pv@grandmedical.test", Assert.Single(request.To).Email);
        Assert.Equal(Rep, Assert.Single(request.Cc).LineManagerOf);
        Assert.Equal("Hastada baş dönmesi", request.Variables["adverseEventNote"]);
        var row = Assert.Single(n.Rows.Items);
        Assert.Equal(VisitNotificationStatuses.Sent, row.Status);
        Assert.DoesNotContain("pv@grandmedical.test", System.Text.Json.JsonSerializer.Serialize(row)); // the address is never stored

        // another tenant setting without the PV mailbox: the manager alone, "route-pending"
        var w2 = new World();
        var n2 = Wire(w2);
        var plan2 = w2.DoctorPlan();
        var body2 = w2.Complete(plan2);
        body2.Safety!.AdverseEvent = true;
        body2.Safety.AdverseEventNote = "Bulantı";
        body2.Safety.OffLabelRequest = true;
        Assert.True((await w2.Handlers().Handle(new SubmitVisitReportV2Command(plan2.Id, body2, null), default)).IsSuccessful);
        Assert.Equal(new[] { VisitNotificationEvents.AdverseEvent, VisitNotificationEvents.OffLabelRequest, VisitNotificationEvents.ReportSubmitted },
            n2.Rows.Items.Select(r => r.EventCode).OrderBy(x => x));
        var adverse = n2.Rows.Items.Single(r => r.EventCode == VisitNotificationEvents.AdverseEvent);
        Assert.Equal(VisitNotificationStatuses.RoutePending, adverse.Status);
        Assert.Contains(VisitNotificationNotes.PvRoutePending, adverse.Notes);
        var adverseRequest = n2.Platform.Requests.Single(r => r.EventCode == VisitNotificationEvents.AdverseEvent);
        Assert.Equal(Rep, Assert.Single(adverseRequest.To).LineManagerOf);        // the manager moves up to "to"
        Assert.Empty(adverseRequest.Cc);
        var offLabel = n2.Rows.Items.Single(r => r.EventCode == VisitNotificationEvents.OffLabelRequest);
        Assert.Equal((VisitNotificationStatuses.RoutePending, true), (offLabel.Status, offLabel.Notes.Contains(VisitNotificationNotes.MslRoutePending)));
    }

    [Fact]
    public async Task W5_a_pharmacy_report_has_no_off_label_mail_and_a_failing_platform_or_store_never_fails_the_report()
    {
        var w = new World();
        var n = Wire(w);
        var plan = w.PharmacyPlan();
        var body = new VisitReportV2Body(new VisitReportGeneral { VisitKindCode = "in_person", MetRoleCode = "pharmacist" }, null, null,
            new VisitReportPharmacy { Sales = { new VisitReportSalesRow { GskuId = Guid.NewGuid(), TrendCode = "up" } } },
            new VisitReportSafety { AdverseEvent = false, OffLabelRequest = true }, new VisitReportFollowUp());
        var pharmacy = await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default);
        Assert.True(pharmacy.IsSuccessful, string.Join(",", pharmacy.Errors ?? []) + " " + string.Join(",", pharmacy.Data?.Missing.Select(m => m.Field) ?? []));
        Assert.Equal(new[] { VisitNotificationEvents.ReportSubmitted }, n.Rows.Items.Select(r => r.EventCode));

        // the Platform down: the report is Submitted, the row "failed" (retried later)
        var w2 = new World();
        var n2 = Wire(w2);
        n2.Platform.Down = true;
        var plan2 = w2.DoctorPlan();
        var res = await w2.Handlers().Handle(new SubmitVisitReportV2Command(plan2.Id, w2.Complete(plan2), null), default);
        Assert.Equal((201, VisitReportStatus.Submitted), (res.StatusCode, w2.Reports.Items.Single().ReportStatus));
        var failed = Assert.Single(n2.Rows.Items);
        Assert.Equal((VisitNotificationStatuses.Failed, 1), (failed.Status, failed.Attempts));
        Assert.Equal((w2.Clock.GetUtcNow() + TimeSpan.FromMinutes(5)).UtcTicks, failed.NextAttemptTicks);

        // the Platform client throwing, or the log store throwing: the report still stands (K-N8)
        foreach (var breakIt in new Action<W5Notifications>[] { x => x.Platform.Throws = true, x => x.Rows.ThrowOnInsert = true })
        {
            var w3 = new World();
            var n3 = Wire(w3);
            breakIt(n3);
            var plan3 = w3.DoctorPlan();
            var ok = await w3.Handlers().Handle(new SubmitVisitReportV2Command(plan3.Id, w3.Complete(plan3), null), default);
            Assert.True(ok.IsSuccessful, string.Join(",", ok.Errors ?? []));
            Assert.Equal(VisitReportStatus.Submitted, w3.Reports.Items.Single().ReportStatus);
        }
    }
}
