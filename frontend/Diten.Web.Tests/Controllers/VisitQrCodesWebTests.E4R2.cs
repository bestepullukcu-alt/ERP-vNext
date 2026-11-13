using System.Text.Json;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>WP-VW-W3E · W3E-E4R2-FIX-a (E2-1) on the institution QR codes list, on the REAL index.js in the fake factory: the QR
/// status (the page's own content) stays on a narrow screen before the type, the city, the version and the scan columns.</summary>
public sealed partial class VisitQrCodesWebTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void E2_1_the_qr_status_stays_on_a_narrow_screen_before_the_type(bool manage)
    {
        var node = RunNode(ListHarness(manage));
        if (node is null) { return; }
        var p = JsonDocument.Parse(node).RootElement.GetProperty("options").GetProperty("priorities");
        int N(int t) => p.GetProperty(t.ToString(System.Globalization.CultureInfo.InvariantCulture)).GetInt32();
        // control 0 · checkbox 1 · institution 2 · type 3 · city 4 · status 5 · version 6 · last scan 7 · recent 8 · actions -1
        Assert.Equal("all", p.GetProperty("-1").GetString());
        Assert.True(N(5) < N(3), "the QR status before the type (E2-1)");
        Assert.True(N(3) < N(4) && N(4) < N(6) && N(6) < N(7) && N(7) < N(8), "then the type, the city, the version, the scans");
    }
}
