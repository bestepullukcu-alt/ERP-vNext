using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX5 item 4: the same correction is the same request however its quantity was written. The
/// maker types "2.50"; the screen's "Repeat request" sends MDM's number back as "2.5". FIX4 derived the operation from
/// the decimal's own scale, so the repeat was ANOTHER operation (MDM: 409) and never reached the stuck request.
/// </summary>
public sealed class GskuFix5WebTests
{
    private const string Gateway = "https://gateway-fixture.invalid";
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Maker = Guid.NewGuid();
    private static readonly Guid Gsku = Guid.NewGuid();

    [Theory]
    [InlineData("2.50", "2.5")]
    [InlineData("10", "10.000")]
    [InlineData("0.0000001", "0.00000010")]
    public async Task A_correction_written_two_ways_reaches_MDM_as_the_same_operation(string typed, string repeated)
    {
        var first = await CorrectionKeyAsync(typed);
        var again = await CorrectionKeyAsync(repeated);

        Assert.Equal(first, again);
    }

    // FIX6 item 5 (e): the draft edit derives its operation the same way.
    [Theory]
    [InlineData("2.50", "2.5")]
    [InlineData("10", "10.000")]
    public async Task A_draft_edit_written_two_ways_reaches_MDM_as_the_same_operation(string typed, string repeated)
    {
        Assert.Equal(await EditKeyAsync(typed), await EditKeyAsync(repeated));
    }

    [Fact]
    public async Task Another_quantity_is_another_operation()
    {
        Assert.NotEqual(await CorrectionKeyAsync("2.5"), await CorrectionKeyAsync("2.6"));
    }

    [Theory]
    [InlineData("2.50", "2.5")]
    [InlineData("10.000", "10")]
    [InlineData("0.00000010", "0.0000001")]
    [InlineData("123456789.123456789", "123456789.123456789")]
    public void The_canonical_quantity_drops_trailing_zeros_and_never_has_an_exponent(string value, string canonical)
    {
        Assert.Equal(canonical, GskusController.CanonicalQuantity(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }

    // FIX6 item 3: another user's repeat (or a stale version) is refused by MDM with *_OPERATION_CONFLICT; the screen says
    // "changed elsewhere" — the sentence exists in all seven languages, for the correction and the retirement request.
    [Theory]
    [InlineData("GSKU_CORRECTION_OPERATION_CONFLICT")]
    [InlineData("GSKU_RETIREMENT_REQUEST_OPERATION_CONFLICT")]
    public void Another_users_repeat_is_told_the_record_changed_elsewhere_in_all_seven_languages(string code)
    {
        Assert.Equal("RefusalChangedElsewhere", Diten.Web.Views.MasterDataManagement.Gskus.GskuRefusalSentences.Keys[code]);
        foreach (var language in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resx = System.Xml.Linq.XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "MasterDataManagement",
                "Gskus", $"GskuRefusals.{language}.resx"));
            var sentence = resx.Root!.Elements("data")
                .SingleOrDefault(data => (string?)data.Attribute("name") == "RefusalChangedElsewhere")?.Element("value")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(sentence), $"{language}: RefusalChangedElsewhere");
        }
    }

    private static string WebRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "frontend", "Diten.Web");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("frontend/Diten.Web not found.");
    }

    private static async Task<string> CorrectionKeyAsync(string packQuantity)
    {
        var wire = new KeyRecorder();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", Tenant.ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", Maker.ToString("D")),
                new Claim("permission", "mdm.gskus.request-correction")
            ], "fixture"))
        };
        // Synthetic fixture credential: only seen by the recording handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-gsku-fix5";
        context.Request.Method = "POST";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        var form = $"ExpectedGskuVersion=3&PackQuantity={Uri.EscapeDataString(packQuantity)}&PackUomCode=C62&__RequestVerificationToken=synthetic";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var controller = new GskusController(new HttpClient(wire), gateway, new EphemeralDataProtectionProvider(),
            new Echo<Diten.Web.SharedResource>(), new Echo<GskusIndex>(), NullLogger<GskusController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };

        await controller.RequestCorrection(Gsku, 3, packQuantity, "C62", CancellationToken.None);

        return Assert.Single(wire.Keys);
    }

    private static async Task<string> EditKeyAsync(string packQuantity)
    {
        var wire = new KeyRecorder();
        var controller = ControllerFor(wire, "mdm.gskus.update",
            $"ExpectedVersion=3&PackQuantity={Uri.EscapeDataString(packQuantity)}&PackUomCode=C62&__RequestVerificationToken=synthetic");

        await controller.UpdateDraft(Gsku, 3, packQuantity, "C62", CancellationToken.None);

        return Assert.Single(wire.Keys);
    }

    private static GskusController ControllerFor(KeyRecorder wire, string permission, string form)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", Tenant.ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", Maker.ToString("D")),
                new Claim("permission", permission)
            ], "fixture"))
        };
        // Synthetic fixture credential: only seen by the recording handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-gsku-fix6";
        context.Request.Method = "PUT";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        return new GskusController(new HttpClient(wire), gateway, new EphemeralDataProtectionProvider(),
            new Echo<Diten.Web.SharedResource>(), new Echo<GskusIndex>(), NullLogger<GskusController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private sealed class KeyRecorder : HttpMessageHandler
    {
        public List<string> Keys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Keys.Add(request.Headers.GetValues("Idempotency-Key").Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"isSuccessful\":true,\"statusCode\":202,\"data\":{}}", Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class Echo<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
