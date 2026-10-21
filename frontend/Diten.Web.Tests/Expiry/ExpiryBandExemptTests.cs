using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Expiry;

/// <summary>
/// WP-MVP1-FIX-LIVE4 (FU-0176-BAND-EXEMPT + FU-0173-REQ-COUNTER, G1 live run 2026-10-20) — the movement form's FEFO band opens only where the
/// service runs its outbound FEFO check:
/// <list type="bullet">
/// <item>the Web's ONE exempt list (<c>ExpiryBandRules.FEFO_EXEMPT_TYPES</c>, expiry-bands.js) equals the service's production set
/// (<c>ExpiryHookScope.Exempt</c>, FefoRules.cs, through WireNames.cs) value for value — the guard;</item>
/// <item>Node, against the shipped scripts: an exempt type gets no band / reason, a checked type gets them, the reason travels only when
/// asked (expiry-band-exempt.test.mjs); the shared required tracker reaches 9 / 9 with the band open and does not count the reason with it
/// closed (movement-required-counter.test.mjs, jsdom).</item>
/// </list>
/// The movement form offers no type the service checks (its 8 types are exempt or inbound — measured), so the band never opens there today.
/// </summary>
public sealed class ExpiryBandExemptTests
{
    private static readonly string Repo = TraceViewRenderer.RepoRoot();

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Repo, .. parts]));

    /// <summary>The service's exempt set, in wire names, read from its production source.</summary>
    internal static List<string> ServiceExempt()
    {
        var domain = Path.Combine("services", "Diten.InventoryService", "src", "Diten.InventoryService.Domain");
        var rules = Read(domain, "Expiry", "Rules", "FefoRules.cs");
        var block = Regex.Match(rules, @"HashSet<MovementType> Exempt =\s*\[([^\]]*)\];");
        Assert.True(block.Success, "ExpiryHookScope.Exempt not found in FefoRules.cs");
        var wire = Regex.Matches(Read(domain, "Rules", "WireNames.cs"), @"\[MovementType\.(\w+)\] = ""([A-Z_]+)""")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);
        return Regex.Matches(block.Groups[1].Value, @"MovementType\.(\w+)").Select(m => wire[m.Groups[1].Value]).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>The Web's one list, read from the shipped band script.</summary>
    internal static List<string> WebExempt()
    {
        var bands = Read("frontend", "Diten.Web", "wwwroot", "assets", "js", "Expiry", "expiry-bands.js");
        var list = Regex.Match(bands, @"const FEFO_EXEMPT_TYPES = Object\.freeze\(\[([^\]]*)\]\);");
        Assert.True(list.Success, "FEFO_EXEMPT_TYPES not found in expiry-bands.js");
        Assert.Single(Regex.Matches(bands, "FEFO_EXEMPT_TYPES = "));                                   // one list, not two
        return Regex.Matches(list.Groups[1].Value, "'([A-Z_]+)'").Select(m => m.Groups[1].Value).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void The_web_exempt_list_is_the_services_list_value_for_value()
    {
        var service = ServiceExempt();
        Assert.Equal(9, service.Count);
        Assert.Contains("COUNT_LOSS", service);
        Assert.Equal(service, WebExempt());
    }

    [Fact]
    public void The_movement_form_offers_no_type_the_service_checks()
    {
        var types = Regex.Matches(Read("frontend", "Diten.Web", "Models", "Inventory", "MovementRules.cs"), @"public const string \w+ = ""([A-Z_]+)"";")
            .Select(m => m.Groups[1].Value).Where(t => t != "MANUAL_MOVEMENT").ToList();
        var outbound = types.Where(t => t is "COUNT_LOSS" or "SCRAP" or "WRITE_OFF" or "WAREHOUSE_TRANSFER" or "LOCATION_TRANSFER").ToList();
        Assert.Equal(5, outbound.Count);
        Assert.All(outbound, t => Assert.Contains(t, WebExempt()));
    }

    [Theory]
    [InlineData("expiry-band-exempt.test.mjs", 5)]
    [InlineData("movement-required-counter.test.mjs", 2)]
    public async Task The_band_rules_hold_against_the_shipped_scripts(string file, int pass)
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = Repo, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", file));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex($@"^# pass {pass}$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
