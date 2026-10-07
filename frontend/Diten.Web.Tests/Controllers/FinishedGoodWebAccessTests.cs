using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.FinishedGoods;
using Diten.Web.Views.MasterDataManagement.FinishedGoods;
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
/// WP-MDM-FG-ACCEPT-01 FIX1 items 4 and 5, on the real controller (and, for the face, the real view + layout):
/// item 4 — one form attempt is one MDM creation key (server-drawn, bound to the signed-in subject, 30 minutes): the
/// same token always reaches MDM as the same key, another user's or a forged token is refused before MDM, a timeout is
/// 504 and an unreachable gateway 503, both keeping the token so the repeat is a replay;
/// item 5 — UAS-001: without the read key the page answers 403 with the access-denied surface and nothing of the
/// register; without the create key there is no create form; every JSON endpoint without its key is a 403 body that
/// never reaches MDM.
/// </summary>
public sealed class FinishedGoodWebAccessTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string View = "/Views/MasterDataManagement/FinishedGoods/Index.cshtml";
    private const string Read = "mdm.finished-goods.read";
    private const string Create = "mdm.finished-goods.create";
    private static readonly Guid Gsku = Guid.NewGuid();

    // ------------------------------------------------------------------------------------------------ item 4

    [Fact]
    public async Task The_same_form_attempt_reaches_mdm_as_the_same_key_every_time()
    {
        var protection = new EphemeralDataProtectionProvider();
        var maker = Guid.NewGuid();
        var token = IndexToken(protection, maker);
        var wire = new Recorder(HttpStatusCode.Accepted);

        var first = await Controller(protection, wire, maker, Read, Create).Create(Model(), token, CancellationToken.None);
        var again = await Controller(protection, wire, maker, Read, Create).Create(Model(), token, CancellationToken.None);

        Assert.Equal(StatusCodes.Status202Accepted, Status(first));
        Assert.Equal(StatusCodes.Status202Accepted, Status(again));
        Assert.Equal(2, wire.Keys.Count);
        Assert.Equal(wire.Keys[0], wire.Keys[1]);
        Assert.Matches("^[0-9A-F]{64}$", wire.Keys[0]);
        // 202: the same attempt is handed back, so the browser repeats it.
        Assert.Equal(token, TokenIn(again));
    }

    [Fact]
    public async Task Two_form_attempts_are_two_keys_and_a_created_answer_hands_out_a_new_attempt()
    {
        var protection = new EphemeralDataProtectionProvider();
        var maker = Guid.NewGuid();
        var wire = new Recorder(HttpStatusCode.Created);

        var first = await Controller(protection, wire, maker, Read, Create)
            .Create(Model(), IndexToken(protection, maker), CancellationToken.None);
        await Controller(protection, wire, maker, Read, Create)
            .Create(Model(), IndexToken(protection, maker), CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Status(first));
        Assert.Contains("\"success\":true", JsonSerializer.Serialize(((ObjectResult)first).Value));
        Assert.NotEqual(wire.Keys[0], wire.Keys[1]);
        var next = TokenIn(first);
        Assert.False(string.IsNullOrEmpty(next));
        await Controller(protection, wire, maker, Read, Create).Create(Model(), next, CancellationToken.None);
        Assert.Equal(3, wire.Keys.Distinct().Count());
    }

    [Theory]
    [InlineData("another-user")]
    [InlineData("forged")]
    [InlineData("missing")]
    [InlineData("other-purpose")]
    public async Task A_foreign_or_broken_form_attempt_is_refused_before_mdm(string shape)
    {
        var protection = new EphemeralDataProtectionProvider();
        var maker = Guid.NewGuid();
        var token = shape switch
        {
            "another-user" => IndexToken(protection, Guid.NewGuid()),
            "forged" => IndexToken(protection, maker) + "x",
            "missing" => null,
            // A token of another register (the GSKU form) is not a Finished Good attempt.
            _ => protection.CreateProtector("Diten.Web", "MOD-0290", "GskuFormAttempt", "v1").ToTimeLimitedDataProtector()
                .Protect(JsonSerializer.Serialize(new { userSubject = maker.ToString("D"), operationKey = "AB" }), TimeSpan.FromMinutes(5))
        };

        var refused = await Controller(protection, new Unreachable(), maker, Read, Create)
            .Create(Model(), token, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Status(refused));
        Assert.Contains("ErrorInvalidFormAttempt", JsonSerializer.Serialize(((ObjectResult)refused).Value));
    }

    [Fact]
    public async Task A_timeout_is_504_and_an_unreachable_gateway_503_and_both_keep_the_attempt()
    {
        var protection = new EphemeralDataProtectionProvider();
        var maker = Guid.NewGuid();
        var token = IndexToken(protection, maker);

        var timeout = await Controller(protection, new Throwing(new TaskCanceledException("timed out")), maker, Read, Create)
            .Create(Model(), token, CancellationToken.None);
        var unreachable = await Controller(protection, new Throwing(new HttpRequestException("refused")), maker, Read, Create)
            .Create(Model(), token, CancellationToken.None);

        Assert.Equal(StatusCodes.Status504GatewayTimeout, Status(timeout));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Status(unreachable));
        Assert.Equal(token, TokenIn(timeout));
        Assert.Equal(token, TokenIn(unreachable));
    }

    // ------------------------------------------------------------------------------------------------ item 5

    [Fact]
    public void The_index_without_the_read_key_is_403_with_the_register_view_and_no_create_token()
    {
        var controller = Controller(new EphemeralDataProtectionProvider(), new Unreachable(), Guid.NewGuid(), Create);

        var result = controller.Index();

        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.Equal("~" + View, Assert.IsType<ViewResult>(result).ViewName);
        Assert.Null(controller.ViewData["CanCreateFinishedGood"]);
        Assert.Null(controller.ViewData["FinishedGoodFormAttemptToken"]);
    }

    [Fact]
    public void A_reader_without_the_create_key_gets_no_create_permission_and_no_token()
    {
        var controller = Controller(new EphemeralDataProtectionProvider(), new Unreachable(), Guid.NewGuid(), Read);

        controller.Index();

        Assert.Equal(false, controller.ViewData["CanCreateFinishedGood"]);
        Assert.Null(controller.ViewData["FinishedGoodFormAttemptToken"]);
    }

    [Fact]
    public async Task Every_json_endpoint_without_its_key_answers_a_403_body_and_never_reaches_mdm()
    {
        var protection = new EphemeralDataProtectionProvider();
        var maker = Guid.NewGuid();
        var createOnly = Controller(protection, new Unreachable(), maker, Create);
        var readOnly = Controller(protection, new Unreachable(), maker, Read);

        var results = new (string Name, IActionResult Result)[]
        {
            ("List", await createOnly.List(CancellationToken.None)),
            ("Detail", await createOnly.Detail(Guid.NewGuid(), CancellationToken.None)),
            ("GskuSelector", await readOnly.GskuSelector(CancellationToken.None)),
            ("Create", await readOnly.Create(Model(), IndexToken(protection, maker), CancellationToken.None))
        };

        foreach (var (name, result) in results)
            Assert.True(Status(result) == StatusCodes.Status403Forbidden, name);
    }

    [Fact]
    public async Task With_the_layout_the_unauthorized_face_is_the_access_denied_surface_and_only_a_creator_gets_the_form()
    {
        var denied = await LayoutRenderer.RenderAsync(factory, View, "FinishedGoods", Create);
        var reader = await LayoutRenderer.RenderAsync(factory, View, "FinishedGoods", Read,
            new Dictionary<string, object?> { ["CanCreateFinishedGood"] = false });
        var creator = await LayoutRenderer.RenderAsync(factory, View, "FinishedGoods", Read,
            new Dictionary<string, object?> { ["CanCreateFinishedGood"] = true, ["FinishedGoodFormAttemptToken"] = "attempt-fixture" },
            Create);

        Assert.Contains("diten-access-denied", denied);
        Assert.DoesNotContain("dt-finishedgoods", denied);
        Assert.DoesNotContain("datatables-finishedgoods", denied);
        Assert.DoesNotContain("finishedgoods-l10n", denied);
        Assert.DoesNotContain("FinishedGoods/index.js", denied);
        Assert.DoesNotContain("filterCanonicalCode", denied);
        Assert.DoesNotContain("skeleton-loader", denied);
        Assert.DoesNotContain("offcanvasCreateEdit", denied);

        Assert.Contains("finishedgoods-l10n", reader);
        Assert.Contains("FinishedGoods/index.js", reader);
        Assert.DoesNotContain("diten-access-denied", reader);
        Assert.DoesNotContain("offcanvasCreateEdit", reader);
        Assert.DoesNotContain("formAttemptToken", reader);

        Assert.Contains("offcanvasCreateEdit", creator);
        Assert.Contains("value=\"attempt-fixture\"", creator);
    }

    // ------------------------------------------------------------------------------------------------ helpers

    private static CreateFinishedGoodViewModel Model() => new() { GskuId = Gsku };

    private static int Status(IActionResult result) => Assert.IsAssignableFrom<ObjectResult>(result).StatusCode ?? 200;

    private static string? TokenIn(IActionResult result)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(((ObjectResult)result).Value));
        return json.RootElement.TryGetProperty("formAttemptToken", out var token) ? token.GetString() : null;
    }

    private static string IndexToken(IDataProtectionProvider protection, Guid maker)
    {
        var controller = Controller(protection, new Unreachable(), maker, Read, Create);
        controller.Index();
        return Assert.IsType<string>(controller.ViewData["FinishedGoodFormAttemptToken"]);
    }

    private static FinishedGoodsController Controller(
        IDataProtectionProvider protection, HttpMessageHandler wire, Guid subject, params string[] permissions)
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.fg.test" }).Build();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", Guid.Empty.ToString("D").Replace('0', '1')),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", subject.ToString("D")),
                .. permissions.Select(permission => new Claim("permission", permission))
            ], "fixture"))
        };
        // Synthetic fixture credential: only seen by the recording handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-fg-fix1";
        return new FinishedGoodsController(new HttpClient(wire), gateway, protection,
            new Echo<Diten.Web.SharedResource>(), new Echo<FinishedGoodsIndex>(), NullLogger<FinishedGoodsController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private sealed class Recorder(HttpStatusCode status) : HttpMessageHandler
    {
        public List<string> Keys { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Keys.Add(body.RootElement.GetProperty("idempotencyKey").GetString()!);
            // MDM's own wire shape: enums are numbers.
            var json = $"{{\"isSuccessful\":true,\"statusCode\":{(int)status},\"data\":{{\"finishedGoodId\":\"{Guid.NewGuid():D}\",\"canonicalCode\":\"FG-000001\",\"gskuId\":\"{Gsku:D}\",\"lifecycleStatus\":1,\"version\":1,\"codeBindingState\":2,\"bindingReconciliationRequired\":false}}}}";
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Throwing(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw exception;
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("an unauthorized request must not reach MDM");
    }

    private sealed class Echo<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
