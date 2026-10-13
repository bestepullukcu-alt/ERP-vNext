using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers;
using Diten.InventoryService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.InventoryService.Tests.Architecture;

/// <summary>
/// MOD-0173-FU02 route and history guards (K-04, K-11, §14, §15):
/// <list type="bullet">
/// <item>LocationsController serves EXACTLY the 4 frozen v1 + 7 v1.1 operations; no PUT, no DELETE (a location is
/// archived, never removed).</item>
/// <item>Reads carry <c>inventory.locations.read</c>, writes <c>inventory.locations.manage</c> — nothing else.</item>
/// <item>No route writes history, and the location repository only ever inserts / reads history rows.</item>
/// </list>
/// </summary>
public sealed class LocationRouteArchitectureTests
{
    [Fact]
    public void The_location_routes_are_exactly_the_eleven_contract_operations()
    {
        Assert.Equal(
            [
                "GET api/locations",
                "GET api/locations/search",
                "GET api/locations/{id}",
                "GET api/locations/{id}/history",
                "GET api/locations/{id}/path",
                "GET api/locations/{id}/stock-presence",
                "PATCH api/locations/{id}",
                "POST api/locations",
                "POST api/locations/validate",
                "POST api/locations/{id}/bins/bulk",
                "POST api/locations/{id}/status"
            ],
            Routes().Select(r => r.Route).OrderBy(r => r, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void No_location_route_is_put_or_delete()
        => Assert.All(Routes(), r => Assert.DoesNotMatch("^(PUT|DELETE) ", r.Route));

    [Fact]
    public void Reads_need_locations_read_and_writes_need_locations_manage()
    {
        Assert.All(Routes(), r =>
        {
            // validateLocations is a frozen v1 READ that happens to be a POST.
            var isRead = r.Route.StartsWith("GET ", StringComparison.Ordinal) || r.Route == "POST api/locations/validate";
            var expected = isRead ? InventoryPermissions.LocationsRead : InventoryPermissions.LocationsManage;
            Assert.True(r.Permission == expected, $"{r.Route} requires '{r.Permission}', expected '{expected}'.");
        });
    }

    [Fact]
    public void No_route_mentions_history_except_the_read()
        => Assert.Equal(["GET api/locations/{id}/history"], Routes().Select(r => r.Route).Where(r => r.Contains("history", StringComparison.Ordinal)).ToList());

    [Fact]
    public void The_history_collection_is_only_ever_read_or_inserted()
    {
        var repository = SourceScan.Production("Diten.InventoryService.Persistence")
            .Single(f => f.RelativePath.EndsWith("LocationRepository.cs", StringComparison.Ordinal)
                         && !f.RelativePath.EndsWith("ILocationRepository.cs", StringComparison.Ordinal));

        var calls = Regex.Matches(repository.Body, @"_history\s*\.\s*(\w+)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.Contains(call, new[] { "Find", "InsertManyAsync", "CountDocumentsAsync" }));
    }

    [Fact]
    public void No_production_source_deletes_a_location_or_a_history_row()
    {
        var offenders = SourceScan.Production()
            .Where(f => Regex.IsMatch(f.Body, @"_(locations|history)\s*\.\s*(DeleteOne|DeleteMany|FindOneAndDelete)\w*\s*\("))
            .Select(f => f.RelativePath)
            .ToList();

        Assert.True(offenders.Count == 0, "location / history delete found in: " + string.Join(", ", offenders));
    }

    private static IReadOnlyList<(string Route, string? Permission)> Routes()
        => typeof(LocationsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>().Select(h =>
            (
                Route: $"{h.HttpMethods.Single()} api/locations{(string.IsNullOrEmpty(h.Template) ? string.Empty : "/" + h.Template)}",
                Permission: a.GetCustomAttribute<HasPermissionAttribute>()?.Policy?.Split(':').Last()
            )))
            .ToList();
}
