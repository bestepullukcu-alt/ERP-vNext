using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 AC-21 — the allocate endpoint is service to service: no Gateway route names it, no route opens
/// <c>/internal</c>, and the browser application never calls it.
/// </summary>
public sealed class RecordCodeGatewayExposureTests
{
    [Fact]
    public void AC21_no_gateway_route_carries_record_codes_or_internal()
    {
        var ocelot = Path.Combine(RepoPaths.Root(), "gateway", "Diten.ApiGateway", "ocelot.json");

        var routes = RoutesOf(File.ReadAllText(ocelot));

        Assert.NotEmpty(routes);
        Assert.DoesNotContain(routes, route => route.Contains("record-codes", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(routes, route => route.Contains("/internal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AC21_the_web_application_never_calls_the_allocate_endpoint()
    {
        var web = Path.Combine(RepoPaths.Root(), "frontend", "Diten.Web");
        var callers = Directory.EnumerateFiles(web, "*.*", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}")
                           && (file.EndsWith(".cs") || file.EndsWith(".cshtml") || file.EndsWith(".js") || file.EndsWith(".json")))
            .Where(file => File.ReadAllText(file).Contains("record-codes/allocate", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(callers);
    }

    /// <summary>Every upstream and downstream path template in an Ocelot file.</summary>
    internal static IReadOnlyList<string> RoutesOf(string ocelotJson)
    {
        using var document = System.Text.Json.JsonDocument.Parse(ocelotJson);
        return document.RootElement.GetProperty("Routes").EnumerateArray()
            .SelectMany(route => new[] { "UpstreamPathTemplate", "DownstreamPathTemplate" }
                .Where(name => route.TryGetProperty(name, out _))
                .Select(name => route.GetProperty(name).GetString() ?? string.Empty))
            .ToArray();
    }

    [Fact]
    public void AC21_a_route_added_to_a_copy_of_the_gateway_file_is_caught()
    {
        var copy = """{ "Routes": [ { "UpstreamPathTemplate": "/api/internal/record-codes/allocate", "DownstreamPathTemplate": "/api/internal/record-codes/allocate" } ] }""";

        Assert.Contains(RoutesOf(copy), route => route.Contains("record-codes", StringComparison.OrdinalIgnoreCase));
    }
}
