using Diten.CrmService.Infrastructure.Commercial;
using Diten.CrmService.Infrastructure.VisitNotifications;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-FIX-a (G-13) — the three e-mail links CRM writes (W6 order, W6 one-time approval link, W5 visit report) name
/// their tenant, "D" form, on the PRODUCTION link builders: opened signed out, the Web carries it to the sign-in page, so the
/// reader signs in to that tenant (without it: the default tenant → tenant-unavailable locally, "tenant missing" live).
/// </summary>
public sealed class W6FixaLinkTenantTests
{
    private static readonly Guid Tenant = Guid.Parse("97c5e1b2-0000-4000-8000-000000000001");
    private static readonly Guid Order = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Crm:Notifications:WebBaseUrl"] = "https://web.test/" }).Build();

    [Fact]
    public void L1_every_link_carries_its_tenant()
    {
        var preOrders = new ConfigurationPreOrderLinks(Config());
        Assert.Equal("https://web.test/Commercial/PreOrders/Details/" + Order.ToString("D") + "?tenantId=" + Tenant.ToString("D"), preOrders.OrderUrl(Tenant, Order));
        Assert.Equal("https://web.test/Commercial/PreOrders/ApprovalLink?token=a%2Fb%2Bc&tenantId=" + Tenant.ToString("D"), preOrders.ApprovalLinkUrl(Tenant, "a/b+c"));
        var visits = new ConfigurationVisitNotificationLinks(Config());
        Assert.Equal("https://web.test/CRM/VisitReport?plannedVisitId=" + Order.ToString("D") + "&tenantId=" + Tenant.ToString("D"), visits.ReportUrl(Tenant, Order));
    }
}
