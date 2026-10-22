using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.Web.Tests.Security;

/// <summary>
/// WP-MVP1-SEC-PROXY1 architecture guard — every MVC action of Diten.Web whose route template carries a catch-all parameter
/// (<c>{**x}</c>) is a same-origin proxy candidate, and its controller's source must build the gateway path through
/// <c>ProxyPath.TryBuild</c>, inside the action itself or the private method it hands the catch-all to. A new proxy that concatenates
/// <c>{prefix}/{everything}</c> by hand turns this red. Discovery is by reflection on the production assembly (the routes ASP.NET Core
/// will register), the check on the controller's source under <c>frontend/Diten.Web/Controllers/**</c>.
/// </summary>
public sealed class ProxyPathArchitectureGuardTests
{
    /// <summary>
    /// Catch-all actions that are deliberately NOT on the helper yet — each with its reason. Shrinking this list is welcome; growing it
    /// needs a CT decision.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownExceptions = new Dictionary<string, string>
    {
        // same {prefix}/{path} pattern, outside the six proxies of WP-MVP1-SEC-PROXY1 → follow-up FU-SEC-PROXY1-TIMEENTRY
        ["TimeEntryController.Api"] = "FU-SEC-PROXY1-TIMEENTRY",
        ["TimeEntryController.ApprovalsApi"] = "FU-SEC-PROXY1-TIMEENTRY",
        ["TimeEntryController.CategoriesApi"] = "FU-SEC-PROXY1-TIMEENTRY",
        // a same-site redirect (Location header), not a gateway proxy
        ["ContentSetsRedirectController.*"] = "redirect, not a proxy"
    };

    /// <summary>The six proxies wired in WP-MVP1-SEC-PROXY1 — the guard must keep finding them (a discovery that finds nothing is no guard).</summary>
    private static readonly string[] WiredProxies =
    [
        "ESignatureController.ProxyApi",
        "ExpiryController.ProxyRead",
        "ExpiryController.ProxyReplace",
        "ExpiryController.ProxyWrite",
        "QuarantineController.ProxyRead",
        "QuarantineController.ProxyWrite",
        "ReferenceDataController.ProxyApi",
        "TenantsController.LookupsProxy",
        "WorkflowController.ProxyApi"
    ];

    [Fact]
    public void Every_catch_all_action_builds_its_gateway_path_through_ProxyPath_TryBuild()
    {
        var actions = CatchAllActions().ToList();
        var violations = new List<string>();
        foreach (var (controller, method, parameter) in actions)
        {
            var name = $"{controller.Name}.{method.Name}";
            if (KnownExceptions.ContainsKey(name) || KnownExceptions.ContainsKey($"{controller.Name}.*")) continue;

            var source = ControllerSource(controller);
            if (source is null)
            {
                violations.Add($"{name}: source file not found under frontend/Diten.Web/Controllers");
                continue;
            }

            if (!HandsCatchAllToTryBuild(source, method.Name, parameter)) violations.Add($"{name} ({{**{parameter}}}) does not go through ProxyPath.TryBuild");
        }

        Assert.True(violations.Count == 0, "Catch-all proxies outside the path boundary:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void The_guard_finds_the_six_wired_proxies_and_the_known_exceptions()
    {
        var found = CatchAllActions().Select(a => $"{a.Controller.Name}.{a.Method.Name}").Distinct().Order().ToList();
        Assert.All(WiredProxies, proxy => Assert.Contains(proxy, found));
        Assert.Contains("TimeEntryController.Api", found);
        Assert.Contains(found, f => f.StartsWith("ContentSetsRedirectController.", StringComparison.Ordinal));
        // every exception still exists (a stale exception would silently cover a future action of the same name)
        Assert.All(KnownExceptions.Keys.Where(k => !k.EndsWith(".*", StringComparison.Ordinal)), exception => Assert.Contains(exception, found));
    }

    private static IEnumerable<(Type Controller, MethodInfo Method, string Parameter)> CatchAllActions()
    {
        var assembly = typeof(Diten.Web.Controllers.ExpiryController).Assembly;
        foreach (var controller in assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var templates = method.GetCustomAttributes<HttpMethodAttribute>().Select(a => a.Template)
                    .Concat(method.GetCustomAttributes<RouteAttribute>().Select(a => a.Template))
                    .Concat(method.GetCustomAttributes<AcceptVerbsAttribute>().Select(a => a.Route));
                foreach (var template in templates.Where(t => t is not null).Distinct())
                {
                    var match = Regex.Match(template!, @"\{\*\*?(\w+)[^}]*\}");
                    if (match.Success) yield return (controller, method, match.Groups[1].Value);
                }
            }
        }
    }

    private static string? ControllerSource(Type controller)
    {
        var root = Path.Combine(ProxyHarness.RepoRoot(), "frontend", "Diten.Web", "Controllers");
        var file = Directory.EnumerateFiles(root, controller.Name + ".cs", SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains($"{Path.DirectorySeparatorChar}Archive{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        return file is null ? null : File.ReadAllText(file);
    }

    /// <summary>
    /// The action's body, or the body of the private method it passes the catch-all parameter to (one hop: <c>ProxyApi(everything) =>
    /// ProxyWorkflowAsync(everything)</c>), must call <c>ProxyPath.TryBuild(…, &lt;that parameter&gt;, …)</c>, and must not build a
    /// <c>$"…/{parameter…}"</c> path by hand.
    /// </summary>
    private static bool HandsCatchAllToTryBuild(string source, string action, string parameter)
    {
        foreach (var body in Bodies(source, action))
        {
            if (CallsTryBuild(body, parameter)) return true;
            var hop = Regex.Match(body, $@"(\w+)\(\s*{parameter}\s*\)");
            while (hop.Success)
            {
                var callee = hop.Groups[1].Value;
                if (callee != action)
                {
                    foreach (var inner in Bodies(source, callee))
                    {
                        var innerParameter = Regex.Match(source, $@"\b{callee}\s*\(\s*string\??\s+(\w+)\s*\)");
                        if (innerParameter.Success && CallsTryBuild(inner, innerParameter.Groups[1].Value)) return true;
                    }
                }

                hop = hop.NextMatch();
            }
        }

        return false;
    }

    private static bool CallsTryBuild(string body, string parameter)
        => Regex.IsMatch(body, $@"ProxyPath\.TryBuild\([^;]*\b{parameter}\b")
           && !Regex.IsMatch(body, $@"\$""[^""]*/\{{{parameter}\b");

    /// <summary>Every method body named <paramref name="method"/>: from its signature to the next member signature (expression or block body).</summary>
    private static IEnumerable<string> Bodies(string source, string method)
    {
        foreach (Match signature in Regex.Matches(source, $@"(public|private|internal|protected)[^;{{=]*\b{method}\s*\("))
        {
            var start = signature.Index;
            var next = Regex.Match(source[(start + signature.Length)..], @"\n\s*(\[|(public|private|internal|protected)\s)");
            yield return next.Success ? source.Substring(start, signature.Length + next.Index) : source[start..];
        }
    }
}
