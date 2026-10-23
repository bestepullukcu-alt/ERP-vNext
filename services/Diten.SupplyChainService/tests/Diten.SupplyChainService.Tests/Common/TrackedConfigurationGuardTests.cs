using System.Text.Json;
using Xunit;

namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// The tracked configuration is a contract with whoever clones this repository, and nothing tested it.
///
/// Q414 measured what that costs: no tracked config set <c>Loads:ReferenceBaseUrl</c>, so a Loads create
/// answered 503 DEPENDENCY_UNAVAILABLE in every environment built from the tree, and R-4b's golden flow
/// passed only under a lane env override. Q457 applied the owner's fix. Nothing then kept it applied.
///
/// It has already drifted once since. Q457 recorded the value as <c>http://127.0.0.1:5061/</c>; the tracked
/// file now reads <c>5066</c>, because owner decision OD-P1 renumbered SupplyChain from 5061 to 5066 and gave
/// 5061 to CRM. That particular move was correct and was made — but had it been missed, all three modules
/// would have been pointing their reference reads at another service, and the only symptom would have been a
/// 503 from a module nobody was testing live that week. This class is the thing that would have said so.
///
/// So the assertions are not "the keys exist"; they are the two relationships that can rot silently:
/// the three module reference roots must be the service's OWN origin, and the legal-entity root must not be.
/// Q457 flagged pointing at SupplyChain directly rather than through the gateway as a deliberate shared
/// architectural choice across all three modules, outside MVP-6 to revisit — so that choice is what gets
/// encoded here, and if it is ever revisited this class is the place that has to change with it.
/// </summary>
public sealed class TrackedConfigurationGuardTests
{
    private const string Development = "appsettings.Development.json";
    private const string Base = "appsettings.json";

    private static JsonDocument Config(string name)
    {
        var path = Path.Combine(ApiProjectDirectory(), name);
        Assert.True(File.Exists(path), name + " is missing from " + ApiProjectDirectory()
            + ". It is tracked configuration: a clone without it cannot run the service.");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    /// <summary>Read from the source tree, not from bin: the copy in bin is an output, not the contract.</summary>
    private static string ApiProjectDirectory() => Path.Combine(RepositoryRoot(),
        "services", "Diten.SupplyChainService", "src", "Diten.SupplyChainService.Api");

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Section(JsonDocument config, string section)
    {
        Assert.True(config.RootElement.TryGetProperty(section, out var node),
            Development + " has no \"" + section + "\" section. Its reference reader resolves "
            + section + ":ReferenceBaseUrl and answers 503 DEPENDENCY_UNAVAILABLE without it — this is the "
            + "exact shape of Q414, where a clone could not create a Load at all.");
        Assert.True(node.TryGetProperty("ReferenceBaseUrl", out var value),
            section + " has no ReferenceBaseUrl in " + Development + " (Q414).");
        var text = value.GetString();
        Assert.False(string.IsNullOrWhiteSpace(text), section + ":ReferenceBaseUrl is blank.");
        return text!;
    }

    private static Uri AbsoluteHttp(string section, string value)
    {
        Assert.True(Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri!.Scheme is "http" or "https",
            section + ":ReferenceBaseUrl is \"" + value + "\", which is not an absolute http(s) URI. The "
            + "readers test exactly that and throw 503 when it fails, so a relative or malformed value is "
            + "indistinguishable at runtime from no value at all.");
        return uri!;
    }

    /// <summary>The service's own origin, from the one place that defines it.</summary>
    private static Uri OwnOrigin()
    {
        using var config = Config(Base);
        Assert.True(config.RootElement.TryGetProperty("Urls", out var urls),
            Base + " has no \"Urls\", so this guard cannot tell what origin the service listens on.");
        return AbsoluteHttp("Urls", urls.GetString()!);
    }

    [Theory]
    [InlineData("Loads")]
    [InlineData("Returns")]
    [InlineData("Claims")]
    public void Each_module_reference_root_is_set_and_is_the_services_own_origin(string section)
    {
        using var development = Config(Development);
        var uri = AbsoluteHttp(section, Section(development, section));
        var own = OwnOrigin();
        Assert.True(uri.Host == own.Host && uri.Port == own.Port,
            section + ":ReferenceBaseUrl points at " + uri.Host + ":" + uri.Port + " while this service "
            + "listens on " + own.Host + ":" + own.Port + ". All three modules read their references from "
            + "SupplyChain itself (Q457 recorded that as a deliberate shared choice, not a Loads defect), so "
            + "a mismatch means the port moved and the configuration did not follow — which is how OD-P1's "
            + "5061 to 5066 renumbering could have silently pointed these reads at CRM.");
    }

    [Fact]
    public void The_legal_entity_root_is_set_and_is_NOT_this_service()
    {
        using var development = Config(Development);
        var uri = AbsoluteHttp("LegalEntity", Section(development, "LegalEntity"));
        var own = OwnOrigin();
        Assert.False(uri.Host == own.Host && uri.Port == own.Port,
            "LegalEntity:ReferenceBaseUrl points at this service. It must reach MDM: the validator asks MDM "
            + "whether a legal entity is the token tenant's and Active (R-2, PR #134), and a self-reference "
            + "would make every scope check answer from the wrong service.");
    }

    /// <summary>
    /// E-5 (owner decision 2026-10-21, integration plan §2.1): SupplyChain reaches MDM through the Gateway, never MDM's own
    /// port (Core rule 5). The Gateway's port is read from the one port table (<c>.antigravity/rules/ports.md</c>), so a
    /// renumbering there makes this red instead of silently pointing the scope check somewhere else.
    /// </summary>
    [Fact]
    public void The_legal_entity_root_is_the_gateway_from_the_port_table()
    {
        using var development = Config(Development);
        var uri = AbsoluteHttp("LegalEntity", Section(development, "LegalEntity"));
        var ports = File.ReadAllText(Path.Combine(RepositoryRoot(), ".antigravity", "rules", "ports.md"));
        var gateway = System.Text.RegularExpressions.Regex.Match(ports, @"\*\*(?<port>\d{4})\*\*: Gateway");
        Assert.True(gateway.Success, "ports.md no longer names the Gateway port in its '**NNNN**: Gateway' line.");
        Assert.True(uri.IsLoopback && uri.Port == int.Parse(gateway.Groups["port"].Value),
            "LegalEntity:ReferenceBaseUrl is " + uri + ", not the Gateway (port " + gateway.Groups["port"].Value
            + " in ports.md). E-5: MDM is reached through the Gateway; a direct MDM port bypasses it.");
    }

    /// <summary>
    /// The three module roots must agree with each other as well as with the service. Equal to the service's
    /// origin already implies this today, but the relationship is the thing Q457 named, and stating it
    /// separately means a future change that moves all three somewhere else on purpose still has to keep
    /// them together rather than drifting one at a time.
    /// </summary>
    [Fact]
    public void The_three_module_roots_agree_with_each_other()
    {
        using var development = Config(Development);
        var roots = new[] { "Loads", "Returns", "Claims" }
            .Select(s => AbsoluteHttp(s, Section(development, s)))
            .Select(u => u.Host + ":" + u.Port)
            .Distinct()
            .ToArray();
        Assert.True(roots.Length == 1,
            "Loads, Returns and Claims read references from different origins: " + string.Join(", ", roots)
            + ". They observe the same shipments, so one of them is reading from the wrong place.");
    }
}
