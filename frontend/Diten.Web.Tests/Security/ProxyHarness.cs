using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Diten.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.Web.Tests.Security;

/// <summary>
/// WP-MVP1-SEC-PROXY1 — drives the six production catch-all proxies (<c>…/api/{**everything}</c>) against a recording FakeGateway.
/// Each proxy gets the caller shape it needs to reach its gateway call (token cookie + tenant claim; ReferenceData its read key;
/// Tenants a platform actor) — claims only, no signed token. What the tests observe is what leaves the Web: the gateway URL, or nothing.
/// </summary>
internal static class ProxyHarness
{
    public const string Gateway = "http://gateway.test";

    /// <summary>The six proxies: name, gateway prefix, the verbs their <c>{**everything}</c> actions accept.</summary>
    public static readonly IReadOnlyDictionary<string, (string Prefix, string[] Verbs)> Proxies = new Dictionary<string, (string, string[])>
    {
        ["expiry"] = ("/api/expiry", ["GET", "POST", "PUT"]),
        ["quarantine"] = ("/api/quarantine", ["GET", "POST", "PUT", "PATCH"]),
        ["esignature"] = ("/api/v1/esign", ["GET", "POST", "PUT"]),
        ["referencedata"] = ("/api/v1/reference-data", ["GET", "POST", "PUT", "PATCH", "DELETE"]),
        ["workflow"] = ("/api/v1/workflow", ["GET", "POST", "PUT", "PATCH", "DELETE"]),
        ["tenants"] = ("/api/lookups", ["GET"])
    };

    public static async Task<IActionResult> Proxy(string module, string verb, FakeGateway gateway, string? everything, string query = "")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var http = Context(module, verb, query);
        var context = new ControllerContext { HttpContext = http };
        switch (module)
        {
            case "expiry":
                var expiry = new ExpiryController(new Factory(gateway), new HttpClient(gateway), configuration, new NoKeys(),
                    new KeyLocalizer<Diten.Web.Views.Expiry.Expiry>(), NullLogger<ExpiryController>.Instance) { ControllerContext = context };
                return verb switch { "GET" => await expiry.ProxyRead(everything), "PUT" => await expiry.ProxyReplace(everything), _ => await expiry.ProxyWrite(everything) };
            case "quarantine":
                var quarantine = new QuarantineController(new Factory(gateway), new HttpClient(gateway), configuration, new NoKeys(),
                    new KeyLocalizer<Diten.Web.Views.Quarantine.Quarantine>(), NullLogger<QuarantineController>.Instance) { ControllerContext = context };
                return verb == "GET" ? await quarantine.ProxyRead(everything) : await quarantine.ProxyWrite(everything);
            case "esignature":
                return await new ESignatureController(new Factory(gateway), configuration, NullLogger<ESignatureController>.Instance) { ControllerContext = context }
                    .ProxyApi(everything);
            case "referencedata":
                return await new ReferenceDataController(new Factory(gateway), configuration, NullLogger<ReferenceDataController>.Instance) { ControllerContext = context }
                    .ProxyApi(everything);
            case "workflow":
                return await new WorkflowController(new Factory(gateway), configuration, NullLogger<WorkflowController>.Instance) { ControllerContext = context }
                    .ProxyApi(everything);
            case "tenants":
                return await new TenantsController(new HttpClient(gateway), configuration) { ControllerContext = context }.LookupsProxy(everything!);
            default:
                throw new ArgumentOutOfRangeException(nameof(module), module, null);
        }
    }

    private static DefaultHttpContext Context(string module, string verb, string query)
    {
        var claims = new List<Claim>
        {
            new("tenantId", Guid.NewGuid().ToString()),
            new("sub", Guid.NewGuid().ToString()),
            new("legal_entity_id", Guid.NewGuid().ToString())
        };
        if (module == "referencedata") claims.Add(new Claim("permission", "Platform.BusinessReferenceData.Read"));
        if (module == "tenants") claims.Add(new Claim("actor_type", "platform_admin"));

        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Method = verb;
        http.Request.Headers.Cookie = "access_token=caller-token";
        if (!string.IsNullOrEmpty(query)) http.Request.QueryString = new QueryString(query);
        if (verb != "GET")
        {
            var bytes = Encoding.UTF8.GetBytes("{}");
            http.Request.Body = new MemoryStream(bytes);
            http.Request.ContentLength = bytes.Length;
            http.Request.ContentType = "application/json";
        }

        return http;
    }

    public static int Status(IActionResult result) => result switch
    {
        ContentResult content => content.StatusCode ?? 200,
        ObjectResult obj => obj.StatusCode ?? 200,
        JsonResult json => json.StatusCode ?? 200,
        StatusCodeResult code => code.StatusCode,
        _ => 200
    };

    /// <summary>The 400 body's <c>error.code</c> and <c>error.details.reason</c>.</summary>
    public static (string? Code, string? Reason) Refusal(IActionResult result)
    {
        if (result is not ObjectResult { Value: not null } obj) return (null, null);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(obj.Value));
        if (!json.RootElement.TryGetProperty("error", out var error)) return (null, null);
        var code = error.TryGetProperty("code", out var c) ? c.GetString() : null;
        var reason = error.TryGetProperty("details", out var d) && d.TryGetProperty("reason", out var r) ? r.GetString() : null;
        return (code, reason);
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    /// <summary>
    /// Legitimate rests below <paramref name="prefix"/>, read from the production route sources: contract <c>paths</c> (a YAML file
    /// whose <c>servers</c> url is the prefix, or whose path keys carry it) and the Platform service's <c>[Route]</c> + <c>[HttpX]</c>
    /// attributes. Template parameters become sample values (a GUID for ids, a code for codes).
    /// </summary>
    public static IEnumerable<(string Verb, string Tail)> ContractRests(string yamlFile, string? keyPrefix)
    {
        var lines = File.ReadAllLines(Path.Combine(RepoRoot(), "docs", "analysis", "contracts", yamlFile));
        string? current = null;
        foreach (var line in lines)
        {
            var key = Regex.Match(line, @"^  (/[^ :]*(?::[^ :/]+)?):\s*$");
            if (key.Success)
            {
                current = key.Groups[1].Value;
                continue;
            }

            var verb = Regex.Match(line, @"^    (get|post|put|patch|delete):");
            if (current is null || !verb.Success) continue;
            var path = current;
            if (keyPrefix is not null)
            {
                if (!path.StartsWith(keyPrefix + "/", StringComparison.Ordinal)) continue;
                path = path[keyPrefix.Length..];
            }

            yield return (verb.Groups[1].Value.ToUpperInvariant(), Sample(path.TrimStart('/')));
        }
    }

    public static IEnumerable<(string Verb, string Tail)> ServiceRests(string routePrefix)
    {
        var dir = Path.Combine(RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.API", "Controllers");
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var route = Regex.Match(source, @"^\[Route\(""([^""]+)""\)\]", RegexOptions.Multiline);
            if (!route.Success) continue;
            var classRoute = route.Groups[1].Value.Trim('/');
            foreach (Match action in Regex.Matches(source, @"\[Http(Get|Post|Put|Patch|Delete)\(""([^""]*)""\)\]"))
            {
                var full = $"{classRoute}/{action.Groups[2].Value}".TrimEnd('/');
                if (!full.StartsWith(routePrefix.Trim('/') + "/", StringComparison.Ordinal)) continue;
                yield return (action.Groups[1].Value.ToUpperInvariant(), Sample(full[(routePrefix.Trim('/').Length + 1)..]));
            }
        }
    }

    private static string Sample(string template) => Regex.Replace(template, @"\{(\w+)(:[^}]*)?\}", m =>
    {
        var name = m.Groups[1].Value;
        if (m.Groups[2].Value.Contains("guid") || name.EndsWith("Id", StringComparison.Ordinal) || name == "id")
            return "0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f";
        return name switch
        {
            "code" => "APPROVE",
            "setCode" => "pack-applicability",
            "lookupKey" => "countries",
            "subjectType" => "LOT",
            _ => "sample_1"
        };
    });

    internal sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class NoKeys : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => false;
        public IReadOnlyCollection<string> Keys => [];
    }

    internal sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    internal sealed class FakeGateway : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }
}
