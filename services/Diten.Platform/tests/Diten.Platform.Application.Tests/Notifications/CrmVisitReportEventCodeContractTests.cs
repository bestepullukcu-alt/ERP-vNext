using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Application.Features.Notifications;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-VW-W5 W5-FIX (2) — the contract lock: the four CRM visit report event codes, as literal strings, on the Platform
/// side. CRM pins the SAME four strings (VwW5NotificationsTests.F1, VisitNotificationEvents): if either side changes one,
/// one of the two tests is red — before a single e-mail is refused in production (403) or synced as Draft.
/// </summary>
public sealed class CrmVisitReportEventCodeContractTests
{
    private static readonly string[] Pinned =
    [
        "crm.visitreport.submitted",
        "crm.visitreport.adverseevent",
        "crm.visitreport.offlabelrequest",
        "crm.visitreport.overdue"
    ];

    [Fact]
    public void The_crm_manifest_declares_exactly_the_four_pinned_codes()
    {
        var declared = new CrmManifestProvider().GetManifest().NotificationEvents!.Select(e => e.EventCode).ToList();

        Assert.Equal(Pinned, declared);
        Assert.Equal(Pinned, new[]
        {
            CrmManifestProvider.VisitReportSubmittedEvent,
            CrmManifestProvider.VisitReportAdverseEventEvent,
            CrmManifestProvider.VisitReportOffLabelRequestEvent,
            CrmManifestProvider.VisitReportOverdueEvent
        });
    }

    [Fact]
    public void Each_pinned_code_is_valid_for_the_catalogue_and_is_its_own_template_key()
    {
        var events = new CrmManifestProvider().GetManifest().NotificationEvents!;

        Assert.All(Pinned, code => Assert.True(NotificationParsing.IsValidTemplateKey(code), code));
        Assert.All(events, e => Assert.Equal(e.EventCode, e.DefaultTemplateKey));
    }
}
