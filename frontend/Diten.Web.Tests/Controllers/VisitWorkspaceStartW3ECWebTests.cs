using System.Text;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-BE-c — the Web proxies: (D-2) the start's appVersion is the browser's own short summary of its
/// User-Agent (deterministic, ≤ 40, unknown → "Web"; the page's appVersion is ignored); (§2.10) the QR pre-check is proxied
/// as JSON with the start's key, nothing else accepted.
/// </summary>
public sealed partial class VisitWorkspaceStartWebTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "Safari 18 · iPadOS")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15", "Safari 18 · macOS")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36", "Chrome 129 · Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36 Edg/129.0.2792.52", "Edge 129 · Windows")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.6668.81 Mobile Safari/537.36", "Chrome 129 · Android")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/26.0 Chrome/122.0.0.0 Mobile Safari/537.36", "Samsung Internet 26 · Android")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14.6; rv:131.0) Gecko/20100101 Firefox/131.0", "Firefox 131 · macOS")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/129.0.6668.69 Mobile/15E148 Safari/604.1", "Chrome 129 · iOS")]
    [InlineData(null, "Web")]
    [InlineData("", "Web")]
    [InlineData("curl/8.4.0", "Web")]
    [InlineData("<script>alert(1)</script> Bot/1.0", "Web")]
    public void W3E_the_browser_summary_is_short_deterministic_and_never_copies_the_header(string? userAgent, string expected)
    {
        var summary = VisitWorkspaceController.BrowserSummary(userAgent);
        Assert.Equal(expected, summary);
        Assert.True(summary.Length <= 40);
        Assert.Equal(summary, VisitWorkspaceController.BrowserSummary(userAgent)); // the same header, the same text
    }

    [Fact]
    public async Task W3E_the_start_writes_the_requests_browser_summary_as_app_version()
    {
        const string boundary = "----vw3ec";
        var body = "--" + boundary + "\r\nContent-Disposition: form-data; name=\"plannedVisitId\"\r\n\r\n" + Id + "\r\n"
                   + "--" + boundary + "\r\nContent-Disposition: form-data; name=\"appVersion\"\r\n\r\n9.9.9-fake\r\n"
                   + "--" + boundary + "--\r\n";
        var (gateway, controller) = Arrange([ExecRecord], body, "multipart/form-data; boundary=" + boundary);
        controller.HttpContext.Request.Headers.UserAgent =
            "Mozilla/5.0 (iPad; CPU OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";
        Assert.Equal(200, Status(await controller.StartVisit(CancellationToken.None)));

        var sent = Assert.Single(gateway.Requests);
        var parts = await Utf8PartsAsync(sent.ContentType!, Encoding.UTF8.GetBytes(sent.Body!));
        Assert.Equal(new[] { "plannedVisitId", "clientPlatform", "appVersion" }, parts.Select(x => x.Name).ToArray());
        Assert.Equal("Safari 18 · iPadOS", parts.Single(x => x.Name == "appVersion").Value); // the page's "9.9.9-fake" is gone
        Assert.DoesNotContain("9.9.9-fake", sent.Body);
    }

    [Fact]
    public async Task W3E_the_qr_check_is_proxied_as_json_with_the_record_key_only()
    {
        var json = "{\"plannedVisitId\":\"" + Id + "\",\"qrPayload\":\"DQR1.x\"}";
        var (gateway, controller) = Arrange([ExecRecord], json, "application/json");
        Assert.Equal(200, Status(await controller.QrCheck(CancellationToken.None)));
        var sent = Assert.Single(gateway.Requests);
        Assert.Equal(("POST", GatewayUrl + "/api/crm/visit-execution/qr-check", json), (sent.Method, sent.Uri, sent.Body));
        Assert.Equal(TenantId.ToString(), sent.Tenant);

        var (noKeyGateway, noKey) = Arrange([ExecRead, ReportRead, PlanRead], json, "application/json");
        Assert.Equal(403, Status(await noKey.QrCheck(CancellationToken.None)));
        Assert.Empty(noKeyGateway.Requests);

        var (tenantGateway, withTenant) = Arrange([ExecRecord], "{\"plannedVisitId\":\"" + Id + "\",\"tenantId\":\"" + TenantId + "\"}", "application/json");
        Assert.Equal(400, Status(await withTenant.QrCheck(CancellationToken.None)));     // TenantId is server-resolved
        Assert.Empty(tenantGateway.Requests);

        var route = typeof(VisitWorkspaceController).GetMethod(nameof(VisitWorkspaceController.QrCheck))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false).Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>().Single();
        Assert.Equal("api/execution/qr-check", route.Template);
    }

    private static async Task<List<(string Name, string Value)>> Utf8PartsAsync(string contentType, byte[] body)
    {
        var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(contentType).Boundary).Value!;
        var reader = new MultipartReader(boundary, new MemoryStream(body));
        var parts = new List<(string, string)>();
        while (await reader.ReadNextSectionAsync() is { } section)
        {
            var disposition = ContentDispositionHeaderValue.Parse(section.ContentDisposition);
            using var text = new StreamReader(section.Body, Encoding.UTF8);
            parts.Add((HeaderUtilities.RemoveQuotes(disposition.Name).Value!, await text.ReadToEndAsync()));
        }

        return parts;
    }
}
