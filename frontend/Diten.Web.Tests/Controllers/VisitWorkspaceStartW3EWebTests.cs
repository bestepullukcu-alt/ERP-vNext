using System.Text;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-BE-b (K-W3E-10) — the Web start proxy speaks for a browser, which cannot vouch for its device: whatever
/// the page sends, the CRM receives <c>clientPlatform=web</c> and NONE of the device verification claims (mock location,
/// integrity provider / verdict / token); the photo (bytes, file name, type) and the location fields reach it unchanged.
/// </summary>
public sealed partial class VisitWorkspaceStartWebTests
{
    [Fact]
    public async Task W3E_the_web_start_always_goes_on_as_web_without_any_device_verification_claim()
    {
        const string boundary = "----vw3eboundary";
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0xFF, 0xD9 };
        string Field(string name, string value) => "--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + name + "\"\r\n\r\n" + value + "\r\n";
        var head = Field("plannedVisitId", Id) + Field("clientPlatform", "android") + Field("appVersion", "3.4.0")
                   + Field("latitude", "41.0123") + Field("longitude", "28.9876") + Field("accuracyMeters", "15")
                   + Field("mockLocationDetected", "false") + Field("integrityProvider", "play_integrity")
                   + Field("integrityVerdict", "passed") + Field("integrityToken", "SECRET-TOKEN-xyz")
                   + "--" + boundary + "\r\nContent-Disposition: form-data; name=\"photo\"; filename=\"visit-photo.jpg\"\r\nContent-Type: image/jpeg\r\n\r\n";
        var tail = "\r\n" + Field("photoCapturedAt", "2026-10-14T08:31:00.000Z") + "--" + boundary + "--\r\n";
        var original = Encoding.Latin1.GetBytes(head).Concat(jpeg).Concat(Encoding.Latin1.GetBytes(tail)).ToArray();

        var (body, type) = await VisitWorkspaceController.WebStartBodyAsync(original, boundary, CancellationToken.None);
        var parts = await PartsAsync(type.ToString(), Encoding.Latin1.GetString(body));
        var names = parts.Select(x => x.Name).ToArray();

        // W3E-BE-c (D-2): the page's appVersion is dropped too; the proxy writes the browser summary ("Web" with no User-Agent)
        Assert.Equal(new[] { "plannedVisitId", "latitude", "longitude", "accuracyMeters", "photo", "photoCapturedAt", "clientPlatform", "appVersion" }, names);
        Assert.Equal("Web", Assert.Single(parts, x => x.Name == "appVersion").Value);
        Assert.Equal("web", Assert.Single(parts, x => x.Name == "clientPlatform").Value);          // the client's "android" is overwritten
        foreach (var dropped in new[] { "mockLocationDetected", "integrityProvider", "integrityVerdict", "integrityToken" })
            Assert.DoesNotContain(dropped, names);
        Assert.DoesNotContain("SECRET-TOKEN", Encoding.Latin1.GetString(body));
        Assert.DoesNotContain("passed", Encoding.Latin1.GetString(body));

        Assert.Equal(("41.0123", "28.9876", "15"), (parts[1].Value, parts[2].Value, parts[3].Value));
        var photo = parts[4];
        Assert.Equal(("visit-photo.jpg", "image/jpeg"), (photo.FileName, photo.ContentType));
        Assert.Equal(jpeg, photo.Bytes);                                                          // binary intact
        Assert.Equal("2026-10-14T08:31:00.000Z", parts[5].Value);
        Assert.Equal(new[] { "clientPlatform", "mockLocationDetected", "integrityProvider", "integrityVerdict", "integrityToken", "appVersion" }, VisitWorkspaceController.WebStartDroppedFields);
    }

    [Fact]
    public async Task W3E_the_controller_sends_the_rewritten_body_and_still_needs_a_boundary()
    {
        const string boundary = "----vw3eb2";
        var body = "--" + boundary + "\r\nContent-Disposition: form-data; name=\"plannedVisitId\"\r\n\r\n" + Id + "\r\n"
                   + "--" + boundary + "\r\nContent-Disposition: form-data; name=\"integrityVerdict\"\r\n\r\npassed\r\n"
                   + "--" + boundary + "--\r\n";
        var (gateway, controller) = Arrange([ExecRecord], body, "multipart/form-data; boundary=" + boundary);
        Assert.Equal(200, Status(await controller.StartVisit(CancellationToken.None)));
        var sent = Assert.Single(gateway.Requests);
        var parts = await PartsAsync(sent.ContentType!, sent.Body!);
        Assert.Equal(new[] { "plannedVisitId", "clientPlatform", "appVersion" }, parts.Select(x => x.Name).ToArray());

        var (noBoundaryGateway, noBoundary) = Arrange([ExecRecord], body, "multipart/form-data");
        Assert.Equal(400, Status(await noBoundary.StartVisit(CancellationToken.None)));
        Assert.Empty(noBoundaryGateway.Requests);

        // the start keeps reading the raw body (the form binder never consumes it)
        var start = typeof(VisitWorkspaceController).GetMethod(nameof(VisitWorkspaceController.StartVisit))!;
        Assert.Contains(start.GetCustomAttributes(true), a => a is Diten.Web.Filters.DisableFormValueModelBindingAttribute);
    }

    private sealed record Part(string Name, string Value, string? FileName, string? ContentType, byte[] Bytes);

    private static async Task<List<Part>> PartsAsync(string contentType, string body)
    {
        var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(contentType).Boundary).Value!;
        var reader = new MultipartReader(boundary, new MemoryStream(Encoding.Latin1.GetBytes(body)));
        var parts = new List<Part>();
        while (await reader.ReadNextSectionAsync() is { } section)
        {
            var disposition = ContentDispositionHeaderValue.Parse(section.ContentDisposition);
            using var ms = new MemoryStream();
            await section.Body.CopyToAsync(ms);
            var bytes = ms.ToArray();
            parts.Add(new Part(HeaderUtilities.RemoveQuotes(disposition.Name).Value!, Encoding.UTF8.GetString(bytes),
                HeaderUtilities.RemoveQuotes(disposition.FileName).Value, section.ContentType, bytes));
        }

        return parts;
    }
}
