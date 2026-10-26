using Diten.Web.Services.Proxy;
using Xunit;
using static Diten.Web.Tests.Security.ProxyHarness;

namespace Diten.Web.Tests.Security;

/// <summary>
/// WP-MVP1-SEC-PROXY1 — the path boundary of the six same-origin catch-all proxies, measured on the production actions. The CT finding:
/// <c>/Expiry/api/..%5Clocations?type=Warehouse</c> answered 200 with InventoryService <c>/api/locations</c>, because the gateway URL
/// <c>{prefix}/{everything}</c> is normalized by <see cref="Uri"/> (dot segments collapse, <c>\</c> becomes <c>/</c>) after the proxy built it.
/// Every attack below must be answered 400 <c>INVALID_PROXY_PATH</c> with NO request to the gateway; every legitimate path (read from the
/// contracts and the Platform service routes) must reach the gateway under the module prefix with the query string unchanged.
/// </summary>
public sealed class ProxyBoundaryTests
{
    /// <summary>The CT handoff set (ExpiryProxyBoundaryTests, 19 traversals; 44 of its 77 cases were red at 9362c0402).</summary>
    public static readonly string[] HandoffTraversals =
    [
        "../inventory/movements",
        "../../inventory/movements",
        "..%2F..%2Finventory%2Fmovements",
        "%2E%2E/%2E%2E/inventory",
        "%2e%2e%2f%2e%2e%2finventory",
        "%252E%252E/%252E%252E/inventory",
        "foo/../../trace/lots",
        "exceptions/%2E%2E/%2E%2E/%2E%2E/inventory/movements",
        "./../inventory",
        "....//inventory",
        "..;/inventory",
        "//gateway-evil/x",
        "///gateway-evil/x",
        "\\..\\inventory",
        "..\\..\\inventory\\movements",
        "%5C..%5Cinventory",
        "http://gateway-evil/x",
        "@gateway-evil/x",
        "exceptions/..%00/../inventory"
    ];

    /// <summary>The CT end-to-end escape, its decoded twin, layered encodings, Unicode look-alikes and the rest of the refusal classes.</summary>
    public static readonly string[] MoreAttacks =
    [
        "..%5Clocations",
        "..\\locations",
        "..%255Clocations",
        Layered(2) + "/locations",
        Layered(3) + "/locations",
        Layered(5) + "/locations",
        Layered(6) + "/locations",
        "exceptions/../policy",
        "exceptions/./policy",
        ".",
        "..",
        "\uFF0E\uFF0E/locations",
        "..\u2215locations",
        "..\uFF3Clocations",
        "%EF%BC%8E%EF%BC%8E/locations",
        "%C0%AE%C0%AE/locations",
        "%u002e%u002e/locations",
        "exceptions%00",
        "\t../locations",
        "exceptions\r\n/x",
        "%2Flocations",
        "a//b",
        "C:/windows",
        "https:%2F%2Fgateway-evil",
        "user@gateway-evil/x",
        "exceptions?x=1",
        "exceptions#frag",
        "exceptions/a b",
        "exceptions/%20"
    ];

    /// <summary>For each attack class one input whose REASON is fixed — the sabotages (s1 dot segments, s2 one decode round, s4 backslash)
    /// must turn these red even where the strict character allow-list would still refuse with another reason.</summary>
    public static readonly IReadOnlyDictionary<string, string> ExpectedReason = new Dictionary<string, string>
    {
        ["../inventory/movements"] = ProxyPath.Reasons.DotSegment,
        ["exceptions/../policy"] = ProxyPath.Reasons.DotSegment,
        ["%252E%252E/%252E%252E/inventory"] = ProxyPath.Reasons.DotSegment,
        [Layered(2) + "/locations"] = ProxyPath.Reasons.DotSegment,
        [Layered(3) + "/locations"] = ProxyPath.Reasons.DotSegment,
        [Layered(5) + "/locations"] = ProxyPath.Reasons.DotSegment,
        [Layered(6) + "/locations"] = ProxyPath.Reasons.TooDeeplyEncoded,
        ["..%5Clocations"] = ProxyPath.Reasons.Backslash,
        ["..\\locations"] = ProxyPath.Reasons.Backslash,
        ["..%255Clocations"] = ProxyPath.Reasons.Backslash,
        ["%5C..%5Cinventory"] = ProxyPath.Reasons.Backslash,
        ["exceptions/..%00/../inventory"] = ProxyPath.Reasons.ControlCharacter,
        ["//gateway-evil/x"] = ProxyPath.Reasons.SchemeOrHost,
        ["http://gateway-evil/x"] = ProxyPath.Reasons.SchemeOrHost,
        ["@gateway-evil/x"] = ProxyPath.Reasons.SchemeOrHost,
        ["%2Flocations"] = ProxyPath.Reasons.EncodedSeparator,
        ["a//b"] = ProxyPath.Reasons.EmptySegment,
        ["\uFF0E\uFF0E/locations"] = ProxyPath.Reasons.DisallowedCharacter,
        ["..\u2215locations"] = ProxyPath.Reasons.DisallowedCharacter,
        ["..\uFF3Clocations"] = ProxyPath.Reasons.DisallowedCharacter
    };

    /// <summary><c>..</c> with every dot percent-encoded, then every <c>%</c> encoded again (<paramref name="layers"/> − 1) times.</summary>
    public static string Layered(int layers)
    {
        var value = "%2E%2E";
        for (var i = 1; i < layers; i++) value = value.Replace("%", "%25");
        return value;
    }

    public static IEnumerable<object[]> Attacks()
    {
        foreach (var (module, (_, verbs)) in Proxies)
        {
            foreach (var verb in verbs.Where(v => v is "GET" or "POST"))
            {
                foreach (var input in HandoffTraversals.Concat(MoreAttacks)) yield return [module, verb, input];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Attacks))]
    public async Task An_attack_path_is_refused_400_and_never_reaches_the_gateway(string module, string verb, string everything)
    {
        var gateway = new FakeGateway();
        var result = await Proxy(module, verb, gateway, everything, "?type=Warehouse");

        Assert.True(gateway.Requests.Count == 0,
            $"{module} {verb} '{everything}' reached the gateway: {string.Join(", ", gateway.Requests.Select(u => u.AbsolutePath))}");
        Assert.Equal(400, Status(result));
        var (code, reason) = Refusal(result);
        Assert.Equal(ProxyPath.InvalidProxyPath, code);
        if (ExpectedReason.TryGetValue(everything, out var expected))
        {
            Assert.Equal(expected, reason);
        }
    }

    [Fact]
    public void The_attack_matrix_covers_all_six_proxies_and_the_CT_escape()
    {
        var cases = Attacks().Select(c => ((string)c[0], (string)c[1], (string)c[2])).ToList();
        Assert.Equal(["esignature", "expiry", "quarantine", "referencedata", "tenants", "workflow"], cases.Select(c => c.Item1).Distinct().Order());
        Assert.Equal(11 * (HandoffTraversals.Length + MoreAttacks.Length), cases.Count);
        Assert.Contains(("expiry", "GET", "..%5Clocations"), cases);
        Assert.Equal(19, HandoffTraversals.Length);
    }

    // ── legitimate paths ───────────────────────────────────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> LegitimatePaths()
    {
        foreach (var (module, verb, rest) in Legitimate().Distinct()) yield return [module, verb, rest];
    }

    /// <summary>
    /// The legitimate rests per proxy: contracts (fefo / quarantine / esign — the Web proxy's prefix is the contract's server url or
    /// path-key prefix), the Platform service routes (reference-data, lookups, workflow), and the paths the Web's own JS calls.
    /// The two non-production reference-data fixture routes with a colon (<c>fixtures/evidence-required:provision</c>,
    /// <c>…/{setId}:retire</c>) are NOT here: no Web client calls them and they are refused by the colon rule — see
    /// <see cref="The_colon_fixture_routes_are_a_known_refusal"/>.
    /// </summary>
    public static IEnumerable<(string Module, string Verb, string Tail)> Legitimate()
    {
        foreach (var (verb, rest) in ContractRests("fefo.openapi.yaml", null)) yield return ("expiry", verb, rest);
        foreach (var (verb, rest) in ContractRests("quarantine.openapi.yaml", null)) yield return ("quarantine", verb, rest);
        foreach (var (verb, rest) in ContractRests("esign.openapi.yaml", "/api/v1/esign")) yield return ("esignature", verb, rest);
        foreach (var (verb, rest) in ServiceRests("api/v1/reference-data").Where(r => !r.Tail.Contains(':')))
            yield return ("referencedata", verb, rest);
        foreach (var (verb, rest) in ServiceRests("api/v1/workflow")) yield return ("workflow", verb, rest);
        foreach (var (verb, rest) in ServiceRests("api/lookups")) yield return ("tenants", verb, rest);

        // the Web's own JS calls (wwwroot/assets/js/**)
        foreach (var rest in new[] { "date-overrides", "evidence/exports", "exceptions", "policy" }) yield return ("expiry", "GET", rest);
        yield return ("quarantine", "GET", "lots/0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f/quality-status");
        yield return ("esignature", "GET", "meanings");
        yield return ("esignature", "GET", "policy/effective");
        yield return ("referencedata", "DELETE", "usage-registrations/bulk");
        yield return ("referencedata", "GET", "usage-registrations");
        yield return ("referencedata", "GET", "sets/pack-applicability/hierarchy");
        yield return ("tenants", "GET", "countries");
        // a single leading slash, a trailing slash and a dot inside a segment stay what they were
        yield return ("expiry", "GET", "/exceptions");
        yield return ("workflow", "GET", "definitions/");
        yield return ("referencedata", "GET", "sets/uom.v2/values");
    }

    [Theory]
    [MemberData(nameof(LegitimatePaths))]
    public async Task A_legitimate_path_is_forwarded_under_the_prefix_with_the_query_unchanged(string module, string verb, string rest)
    {
        var (prefix, verbs) = Proxies[module];
        var sendAs = verbs.Contains(verb) ? verb : verbs.Contains("POST") ? "POST" : "GET";
        const string query = "?set_code=uom&activeOnly=true&q=a%2Fb";
        var gateway = new FakeGateway();

        var result = await Proxy(module, sendAs, gateway, rest, query);

        var sent = Assert.Single(gateway.Requests);
        Assert.Equal("gateway.test", sent.Host);
        Assert.Equal($"{prefix}/{rest.TrimStart('/')}", sent.AbsolutePath);
        Assert.Equal(query, sent.Query);
        Assert.Equal(200, Status(result));
    }

    [Fact]
    public void The_legitimate_catalog_is_read_from_the_sources_and_covers_every_proxy()
    {
        var all = Legitimate().ToList();
        var perModule = all.GroupBy(x => x.Module).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(Proxies.Keys.Order(), perModule.Keys.Order());
        // contract / service route counts — a parser that silently finds nothing must not pass as "all legitimate paths forwarded"
        Assert.True(perModule["expiry"] >= 18 + 4, $"expiry {perModule["expiry"]}");
        Assert.True(perModule["quarantine"] >= 20 + 1, $"quarantine {perModule["quarantine"]}");
        Assert.True(perModule["esignature"] >= 25, $"esignature {perModule["esignature"]}");
        Assert.True(perModule["referencedata"] >= 28, $"referencedata {perModule["referencedata"]}");
        Assert.True(perModule["workflow"] >= 20, $"workflow {perModule["workflow"]}");
        Assert.True(perModule["tenants"] >= 18, $"tenants {perModule["tenants"]}");
    }

    [Fact]
    public async Task An_empty_rest_still_means_the_prefix_itself()
    {
        foreach (var (module, (prefix, _)) in Proxies)
        {
            var gateway = new FakeGateway();
            await Proxy(module, "GET", gateway, module == "tenants" ? "" : null);
            var expected = module == "tenants" ? prefix + "/" : prefix;
            Assert.Equal(expected, Assert.Single(gateway.Requests).AbsolutePath);
        }
    }

    /// <summary>Decision point (evidence): the service declares two non-production fixture routes with a colon; the Web proxy refuses
    /// them (no Web client, no contract). If they are ever needed through the Web this test is where the decision becomes visible.</summary>
    [Fact]
    public async Task The_colon_fixture_routes_are_a_known_refusal()
    {
        var colon = ServiceRests("api/v1/reference-data").Where(r => r.Tail.Contains(':')).Select(r => r.Tail).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["fixtures/evidence-required/0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f:retire", "fixtures/evidence-required:provision"], colon);
        foreach (var rest in colon)
        {
            var gateway = new FakeGateway();
            var result = await Proxy("referencedata", "POST", gateway, rest);
            Assert.Empty(gateway.Requests);
            Assert.Equal(400, Status(result));
        }
    }
}
