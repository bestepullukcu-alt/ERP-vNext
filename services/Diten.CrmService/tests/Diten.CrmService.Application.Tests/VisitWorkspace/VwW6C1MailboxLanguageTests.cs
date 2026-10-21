using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Application.Features.VisitReport.V2;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W6 W6-BE-c1 (user decision B) — the W5 mailboxes (PV / MSL) have no language of their own: their mails go in the
/// language of the rep's selling company's country, through the SAME resolver W6 uses (seat → legal entity → MDM country →
/// one of the seven); anything that cannot be read is the tenant's language. Internal readers (the line manager) never
/// get it. The language is fixed on the row at submit (a scheduler retry has no user to ask).
/// </summary>
public sealed partial class VwW4ReportV2Tests
{
    private static readonly Guid LegalEntity = Guid.Parse("1e000000-0000-4000-8000-0000000000c1");

    private static (W5Notifications N, Seats Seats, Countries Countries) WireWithLanguage(World w, string? country)
    {
        var n = new W5Notifications(w.Clock.GetUtcNow());
        var seats = new Seats();
        var countries = new Countries(country);
        w.Notifier = new VisitReportNotifier(n.Sender(), n.Settings, w.Contacts, w.Accounts, new FixedLinks(), w.Reports, w.Plans, null,
            new ExternalRecipientLanguageResolver(seats, countries));
        return (n, seats, countries);
    }

    [Theory]
    [InlineData("TR", "tr")]
    [InlineData("AE", "ar")]
    [InlineData("RO", null)]
    public async Task W6C1_the_pv_and_msl_mailboxes_read_the_selling_companys_country_language_and_the_manager_keeps_their_own(
        string country, string? language)
    {
        var w = new World();
        var (n, _, _) = WireWithLanguage(w, country);
        n.Settings.Items.Add(new VisitNotificationSettings
        {
            TenantId = Tenant, PvEmail = "pv@grandmedical.test", MslEmail = "msl@grandmedical.test", ReportSummary = false
        });
        var plan = w.DoctorPlan();
        var body = w.Complete(plan);
        body.Safety!.AdverseEvent = true;
        body.Safety.AdverseEventNote = "Baş dönmesi";
        body.Safety.OffLabelRequest = true;

        Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).IsSuccessful);

        var adverse = n.Platform.Requests.Single(r => r.EventCode == VisitNotificationEvents.AdverseEvent);
        var offLabel = n.Platform.Requests.Single(r => r.EventCode == VisitNotificationEvents.OffLabelRequest);
        Assert.Equal(("pv@grandmedical.test", language), (adverse.To.Single().Email, adverse.To.Single().Locale));
        Assert.Equal(("msl@grandmedical.test", language), (offLabel.To.Single().Email, offLabel.To.Single().Locale));
        Assert.Null(adverse.Cc.Single().Locale);                                        // the line manager: their own language
        Assert.All(n.Rows.Items.Where(r => r.EventCode != VisitNotificationEvents.ReportSubmitted), r => Assert.Equal(language, r.ExternalLocale));
    }

    [Fact]
    public async Task W6C1_with_mdm_or_the_seat_unreadable_the_mailbox_takes_the_tenants_language_and_the_report_stands()
    {
        foreach (var (seatsDown, mdmDown) in new[] { (true, false), (false, true) })
        {
            var w = new World();
            var (n, seats, countries) = WireWithLanguage(w, "TR");
            seats.Down = seatsDown;
            countries.Down = mdmDown;
            n.Settings.Items.Add(new VisitNotificationSettings { TenantId = Tenant, PvEmail = "pv@grandmedical.test", ReportSummary = false });
            var plan = w.DoctorPlan();
            var body = w.Complete(plan);
            body.Safety!.AdverseEvent = true;
            body.Safety.AdverseEventNote = "Bulantı";

            Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).IsSuccessful);
            Assert.Null(n.Platform.Requests.Single(r => r.EventCode == VisitNotificationEvents.AdverseEvent).To.Single().Locale);
        }
    }

    [Fact]
    public async Task W6C1_a_scheduler_retry_sends_the_language_fixed_at_submit_without_asking_again()
    {
        var w = new World();
        var (n, seats, _) = WireWithLanguage(w, "FR");
        n.Settings.Items.Add(new VisitNotificationSettings { TenantId = Tenant, PvEmail = "pv@grandmedical.test", ReportSummary = false });
        n.Platform.Down = true;
        var plan = w.DoctorPlan();
        var body = w.Complete(plan);
        body.Safety!.AdverseEvent = true;
        body.Safety.AdverseEventNote = "Bulantı";
        Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).IsSuccessful);

        seats.Down = true;                    // the scheduler has no user: nothing can be asked again
        n.Platform.Down = false;
        var row = n.Rows.Items.Single(r => r.EventCode == VisitNotificationEvents.AdverseEvent);
        await n.Sender().SendAsync(row, default);

        Assert.Equal("fr", n.Platform.Requests.Last().To.Single().Locale);
    }

    internal sealed class Seats : IOrganizationSeatReader
    {
        public bool Down { get; set; }

        public Task<OrganizationSeatResult> GetAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult(Down ? new OrganizationSeatResult(false, null)
                : new OrganizationSeatResult(true, new OrganizationSeat(Guid.NewGuid(), "TR-MEDICAL-REPRESENTATIVE", null, null, LegalEntity, null)));

        public Task<OrganizationSeatBulkResult> LookupAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    internal sealed class Countries(string? country) : ILegalEntityCountryReader
    {
        public bool Down { get; set; }

        public Task<string?> CountryOfAsync(Guid legalEntityId, CancellationToken ct)
            => Task.FromResult(Down || legalEntityId != LegalEntity ? null : country);
    }
}
