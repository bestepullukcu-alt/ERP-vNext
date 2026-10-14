using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Expiry;

/// <summary>
/// WP-MVP1-FIX-WEBGATES v2 (CT finding at the F5 acceptance): the Web key checks of the Expiry data doors were not guarded — removing
/// <c>list/exceptions</c>'s ExceptionsRead, the policy read's PoliciesRead or <c>data/evidence/preview</c>'s EvidenceExport left every
/// Expiry Web test green. Not an open door (the Web carries the caller's token and InventoryService re-checks the key), but an unproven one.
/// <para>The doors are DISCOVERED from the production controller (reflection: every routed action that does not return a page and is not the
/// catch-all proxy). <see cref="Gates"/> is the one table of their keys; it is checked three ways against the production code:
/// (1) completeness — discovered doors = table rows; (2) the keys of each row = the keys of the action's own first
/// <c>if (!_permissions.Has(…)) return Refuse(403, …)</c> statement in ExpiryController.cs; (3) behaviour — without the key 403 and the
/// FakeGateway sees NOTHING, with any one of the keys the request reaches the gateway.</para>
/// The catch-all proxy (<c>api/{**everything}</c>) is the "no Web key — the service authorizes" class: measured here only as "no token →
/// 401 and nothing sent". Its path boundary is WP-MVP1-SEC-PROXY1's (<c>Diten.Web.Tests/Security/**</c>) and is not tested again.
/// </summary>
public sealed class ExpiryDataGateTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly string SampleId = "0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f";

    /// <summary>THE gate table — door action → the keys that open it (any one of them). Read from ExpiryController.cs at cd3680d7c.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Gates = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["ListApproaching"] = [ExpiryKeys.DashboardRead],
        ["ListExpired"] = [ExpiryKeys.DashboardRead],
        ["DashboardData"] = [ExpiryKeys.DashboardRead],
        ["FefoOrderData"] = [ExpiryKeys.FefoOrderRead],
        ["Export"] = [ExpiryKeys.DashboardRead],
        ["Lookups"] = [ExpiryKeys.DashboardRead, ExpiryKeys.FefoOrderRead],
        ["ListExceptions"] = [ExpiryKeys.ExceptionsRead],
        ["ExceptionData"] = [ExpiryKeys.ExceptionsRead],
        ["PolicyData"] = [ExpiryKeys.PoliciesRead],
        ["PolicyProducts"] = [ExpiryKeys.PoliciesRead],
        ["EvidencePreview"] = [ExpiryKeys.EvidenceExport],
        ["EvidenceExports"] = [ExpiryKeys.EvidenceExport],
        ["EvidenceFile"] = [ExpiryKeys.EvidenceExport],
        ["BandContext"] = [InventoryPermissionKeys.CreateMovements, TracePermissionKeys.LotsCreate, ExpiryKeys.DashboardRead],
    };

    /// <summary>Gates INSIDE an opened door: the extra gateway call happens only with the key (door, key, the call's path prefix).</summary>
    public static readonly (string Door, string Key, string Call)[] InnerGates =
    [
        ("DashboardData", ExpiryKeys.PoliciesRead, "/api/expiry/policy"),
        ("DashboardData", ExpiryKeys.ExceptionsRead, "/api/expiry/exceptions"),
    ];

    /// <summary>The "no Web key — the service authorizes" class: the catch-all proxy's actions.</summary>
    public static readonly string[] ServiceAuthorized = ["ProxyRead", "ProxyReplace", "ProxyWrite"];

    /// <summary>The page actions (UAS-001 is the view's — measured, not touched: ExpiryWebTests / ExpiryF5WebTests UAS_001_* render each one).</summary>
    public static readonly string[] Pages = ["Approaching", "Evidence", "ExceptionDetails", "Exceptions", "Expired", "FefoOrder", "Index", "Policy"];

    // ── 1. completeness: discovered = table ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_routed_action_of_ExpiryController_is_in_exactly_one_class_and_every_door_is_in_the_gate_table()
    {
        var actions = Routed().ToList();
        var pages = actions.Where(a => IsPage(a.Method)).Select(a => a.Method.Name).Distinct().Order(StringComparer.Ordinal).ToList();
        var proxies = actions.Where(a => !IsPage(a.Method) && a.Template.Contains("{**", StringComparison.Ordinal)).Select(a => a.Method.Name).Distinct().Order(StringComparer.Ordinal).ToList();
        var doors = actions.Where(a => !IsPage(a.Method) && !a.Template.Contains("{**", StringComparison.Ordinal)).Select(a => a.Method.Name).Distinct().Order(StringComparer.Ordinal).ToList();

        Assert.Equal(Gates.Keys.Order(StringComparer.Ordinal), doors);
        Assert.Equal(ServiceAuthorized, proxies);
        Assert.Equal(Pages, pages);
        Assert.Empty(doors.Intersect(proxies));
    }

    [Fact]
    public void A_page_action_only_returns_its_view()
    {
        var source = Source();
        foreach (var page in Pages)
        {
            var body = Body(source, page);
            Assert.Matches(@"=>\s*View\(", body);
        }
    }

    // ── 2. the table = the production code's own gate statement ────────────────────────────────────────────────

    public static IEnumerable<object[]> Doors() => Gates.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Doors))]
    public void The_table_keys_are_the_keys_of_the_doors_own_403_statement(string door)
    {
        var body = Body(Source(), door);
        var gate = Regex.Match(body, @"if\s*\((?<cond>[^;]*?)\)\s*return\s+Refuse\(403", RegexOptions.Singleline);
        Assert.True(gate.Success, $"{door}: no `if (…) return Refuse(403, …)` statement");
        var keys = KeysIn(gate.Groups["cond"].Value);
        Assert.Equal(Gates[door].Order(StringComparer.Ordinal), keys.Order(StringComparer.Ordinal));
        // the first statement of the door — nothing reaches the gateway before it
        var before = body[..gate.Index];
        Assert.DoesNotContain("SendAsync", before, StringComparison.Ordinal);
        Assert.DoesNotContain("Gateway(", before, StringComparison.Ordinal);

        // every other key check of the door is an inner gate of the table
        var inner = Regex.Matches(body[(gate.Index + gate.Length)..], @"_permissions\.Has\((?<k>[\w.]+)\)").Select(m => Resolve(m.Groups["k"].Value)).ToList();
        Assert.Equal(InnerGates.Where(g => g.Door == door).Select(g => g.Key).Order(StringComparer.Ordinal), inner.Order(StringComparer.Ordinal));
    }

    // ── 3. behaviour ─────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task Without_its_key_a_door_answers_403_and_nothing_reaches_the_gateway(string door)
    {
        // every other key the Web knows — only the door's own keys are missing
        var keys = AllKeys().Except(Gates[door], StringComparer.OrdinalIgnoreCase).ToArray();
        var gateway = new FakeGateway();

        var result = await Invoke(door, gateway, keys);

        Assert.Equal(403, Status(result));
        Assert.True(gateway.Requests.Count == 0, $"{door} without {string.Join(" / ", Gates[door])} sent: {string.Join(", ", gateway.Requests)}");
    }

    public static IEnumerable<object[]> DoorKeys() => Gates.SelectMany(g => g.Value.Select(k => new object[] { g.Key, k }));

    [Theory]
    [MemberData(nameof(DoorKeys))]
    public async Task With_one_of_its_keys_a_door_reaches_the_gateway(string door, string key)
    {
        var gateway = new FakeGateway();

        var result = await Invoke(door, gateway, [key]);

        Assert.NotEqual(403, Status(result));
        Assert.True(gateway.Requests.Count > 0, $"{door} with {key} sent nothing (status {Status(result)})");
    }

    public static IEnumerable<object[]> Inner() => InnerGates.Select(g => new object[] { g.Door, g.Key, g.Call });

    [Theory]
    [MemberData(nameof(Inner))]
    public async Task An_inner_call_happens_only_with_its_key(string door, string key, string call)
    {
        var open = Gates[door][0];
        var without = new FakeGateway();
        await Invoke(door, without, AllKeys().Where(k => k == open || InnerGates.All(g => g.Key != k || g.Door != door)).ToArray());
        Assert.NotEmpty(without.Requests);
        Assert.DoesNotContain(without.Requests, r => r.StartsWith(call, StringComparison.Ordinal));

        var with = new FakeGateway();
        await Invoke(door, with, [open, key]);
        Assert.Contains(with.Requests, r => r.StartsWith(call, StringComparison.Ordinal));
    }

    // ── 4. the service-authorized class ─────────────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> Proxies() => ServiceAuthorized.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Proxies))]
    public async Task The_catch_all_proxy_without_a_token_answers_401_and_sends_nothing(string action)
    {
        var gateway = new FakeGateway();
        var result = await Invoke(action, gateway, AllKeys().ToArray(), token: false);
        Assert.Equal(401, Status(result));
        Assert.Empty(gateway.Requests);

        // positive control: with a token and NO Web key the call is forwarded — the service's [HasPermission] is the authority
        var forwarded = new FakeGateway();
        await Invoke(action, forwarded, []);
        Assert.Equal("/api/expiry/exceptions", Assert.Single(forwarded.Requests).Split('?')[0]);
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<(MethodInfo Method, string Template)> Routed()
    {
        foreach (var method in typeof(ExpiryController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            foreach (var attribute in method.GetCustomAttributes<HttpMethodAttribute>())
                yield return (method, attribute.Template ?? string.Empty);
        }
    }

    /// <summary>A page is a synchronous action (the views are rendered, nothing is fetched); every door is asynchronous.</summary>
    private static bool IsPage(MethodInfo method) => method.ReturnType == typeof(IActionResult);

    private static IEnumerable<string> AllKeys()
        => new[] { typeof(ExpiryKeys), typeof(InventoryPermissionKeys), typeof(TracePermissionKeys) }
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static List<string> KeysIn(string condition)
        => Regex.Matches(condition, @"_permissions\.Has\((?<k>[\w.]+)\)").Select(m => Resolve(m.Groups["k"].Value)).ToList();

    /// <summary><c>ExpiryKeys.X</c> / <c>Diten.Web.Models.Inventory.InventoryPermissionKeys.X</c> → the constant's value.</summary>
    private static string Resolve(string reference)
    {
        var parts = reference.Split('.');
        var (typeName, field) = (parts[^2], parts[^1]);
        var type = new[] { typeof(ExpiryKeys), typeof(InventoryPermissionKeys), typeof(TracePermissionKeys) }.Single(t => t.Name == typeName);
        return (string)type.GetField(field, BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;
    }

    private static string Source()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(), "frontend", "Diten.Web", "Controllers", "ExpiryController.cs"));
    }

    /// <summary>The method's text from its signature to the next member (attribute, doc comment or modifier at member indentation).</summary>
    private static string Body(string source, string method)
    {
        var signature = Regex.Match(source, $@"\n    public [^\n]*\b{method}\(");
        Assert.True(signature.Success, $"{method}: signature not found in ExpiryController.cs");
        var rest = source[(signature.Index + 1)..];
        var next = Regex.Match(rest[1..], @"\n    (\[|///|public |private |internal |protected )");
        return next.Success ? rest[..(next.Index + 1)] : rest;
    }

    private static async Task<IActionResult> Invoke(string action, FakeGateway gateway, string[] keys, bool token = true)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var claims = new List<Claim>
        {
            new("tenantId", "6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b"),
            new("sub", "0d9c8b7a-6f5e-4d3c-2b1a-0f9e8d7c6b5a"),
            new("legal_entity_id", "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d")
        };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Method = action switch { "ProxyWrite" => "POST", "ProxyReplace" => "PUT", _ => "GET" };
        if (token) http.Request.Headers.Cookie = "access_token=caller-token";
        if (http.Request.Method != "GET")
        {
            var bytes = Encoding.UTF8.GetBytes("{}");
            http.Request.Body = new MemoryStream(bytes);
            http.Request.ContentLength = bytes.Length;
            http.Request.ContentType = "application/json";
        }

        var controller = new ExpiryController(new Factory(gateway), new HttpClient(gateway), configuration, new KeySet(keys),
            new KeyLocalizer<Diten.Web.Views.Expiry.Expiry>(), NullLogger<ExpiryController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var method = typeof(ExpiryController).GetMethod(action, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;
        var arguments = method.GetParameters().Select(p => Sample(action, p)).ToArray();
        return await (Task<IActionResult>)method.Invoke(controller, arguments)!;
    }

    /// <summary>A valid value per parameter, so that only the key decides (an unknown parameter fails loudly — a new door needs a sample).</summary>
    private static object? Sample(string action, ParameterInfo parameter) => parameter.ParameterType == typeof(CancellationToken)
        ? CancellationToken.None
        : parameter.Name switch
        {
            "list" => "approaching",
            "kind" => "warehouses",
            "everything" => "exceptions",
            "skuLevel" => "Lsku",
            "id" or "exportId" or "itemId" or "skuId" or "warehouseId" => SampleId,
            _ => throw new InvalidOperationException($"{action}: no sample for parameter '{parameter.Name}' — add one here.")
        };

    private static int Status(IActionResult result) => result switch
    {
        ContentResult content => content.StatusCode ?? 200,
        ObjectResult obj => obj.StatusCode ?? 200,
        JsonResult json => json.StatusCode ?? 200,
        StatusCodeResult code => code.StatusCode,
        _ => 200
    };

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class KeySet(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    /// <summary>Records every request (path + query) and answers 200 <c>{}</c> — the door's own logic decides what it sends.</summary>
    private sealed class FakeGateway : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }
}
