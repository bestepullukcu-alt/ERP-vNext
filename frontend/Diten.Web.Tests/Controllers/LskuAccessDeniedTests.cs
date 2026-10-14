using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers;
using Diten.Web.Views.MasterDataManagement.Lskus;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 B6 (UAS-001) — a signed-in user without mdm.lskus.read gets 403 and the shell's access-denied
/// surface: no redirect (Forbid went to an action that does not exist), and nothing of the register (no filter bar,
/// table, skeleton, l10n block or page script). The JSON endpoints answer a 403 body, never a redirect. The same shape
/// GSKU took in FIX3 B17 (GskuListComponentProxyTests); measured on the real controller and the real view + layout.
/// </summary>
public sealed class LskuAccessDeniedTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string View = "/Views/MasterDataManagement/Lskus/Index.cshtml";

    [Fact]
    public void The_index_without_the_read_permission_answers_403_with_the_register_view_not_a_redirect()
    {
        var controller = Controller("mdm.lskus.create");

        var result = controller.Index();

        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.Equal("~" + View, Assert.IsType<ViewResult>(result).ViewName);
        Assert.Null(controller.ViewData["CanCreateLsku"]);
    }

    [Fact]
    public async Task The_json_endpoints_answer_a_403_body_never_a_redirect()
    {
        var controller = Controller("mdm.lskus.create");

        var list = await controller.List(CancellationToken.None);
        var detail = await controller.Detail(Guid.NewGuid(), CancellationToken.None);

        foreach (var result in new[] { list, detail })
        {
            var body = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, body.StatusCode);
        }
    }

    // LSKU FIX2 item 11: every action endpoint refuses a caller without its key with a 403 body — never a redirect,
    // never a call to MDM (the handler behind is unreachable). The reader key alone opens none of them.
    [Fact]
    public async Task Every_action_endpoint_without_its_key_answers_a_403_body_and_never_reaches_mdm()
    {
        var controller = Controller("mdm.lskus.read");

        var results = new (string Name, IActionResult Result)[]
        {
            ("CreateOptions", await controller.CreateOptions(CancellationToken.None)),
            ("Create", await controller.Create(new Diten.Web.Models.Lskus.CreateLskuViewModel { GskuId = Guid.NewGuid(), MarketCode = "TR" },
                "token", CancellationToken.None)),
            ("Submit", await controller.SubmitLifecycle(Guid.NewGuid(), 0, CancellationToken.None)),
            ("Withdraw", await controller.WithdrawIdentityApproval(Guid.NewGuid(), 1, "X", null, CancellationToken.None)),
            ("RequestRetirement", await controller.RequestRetirement(Guid.NewGuid(), 1, "X", CancellationToken.None)),
            ("Cancel", await controller.CancelDraft(Guid.NewGuid(), 0, "X", CancellationToken.None))
        };

        foreach (var (name, result) in results)
        {
            var body = Assert.IsType<ObjectResult>(result);
            Assert.True(body.StatusCode == StatusCodes.Status403Forbidden, name);
        }
    }

    [Fact]
    public async Task With_the_layout_the_unauthorized_face_is_the_access_denied_surface_and_the_reader_gets_the_list()
    {
        var denied = await LayoutRenderer.RenderAsync(factory, View, "Lskus", "mdm.lskus.create");
        var reader = await LayoutRenderer.RenderAsync(factory, View, "Lskus", "mdm.lskus.read");

        Assert.Contains("diten-access-denied", denied);
        Assert.DoesNotContain("datatables-lskus", denied);
        Assert.DoesNotContain("skeleton-loader", denied);
        Assert.DoesNotContain("lskus-l10n", denied);
        Assert.DoesNotContain("Lskus/index.js", denied);

        Assert.Contains("datatables-lskus", reader);
        Assert.Contains("lskus-l10n", reader);
        Assert.Contains("Lskus/index.js", reader);
        Assert.DoesNotContain("diten-access-denied", reader);
    }

    // The GSKU register keeps the same face (its own tests measure the details; this pins the pair alike).
    [Fact]
    public async Task The_gsku_register_has_the_same_face()
    {
        var denied = await LayoutRenderer.RenderAsync(factory, "/Views/MasterDataManagement/Gskus/Index.cshtml", "Gskus", "mdm.gskus.create");

        Assert.Contains("diten-access-denied", denied);
        Assert.DoesNotContain("dt-gskus", denied);
        Assert.DoesNotContain("skeleton-loader", denied);
    }

    private static LskusController Controller(string permission)
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.lsku.test" }).Build();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", Guid.NewGuid().ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", Guid.NewGuid().ToString("D")),
                new Claim("permission", permission)
            ], "fixture"))
        };
        return new LskusController(new HttpClient(new Unreachable()), gateway, new EphemeralDataProtectionProvider(),
            new EchoLocalizer(), NullLogger<LskusController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("an unauthorized request must not reach MDM");
    }

    private sealed class EchoLocalizer : IStringLocalizer<LskusIndex>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
