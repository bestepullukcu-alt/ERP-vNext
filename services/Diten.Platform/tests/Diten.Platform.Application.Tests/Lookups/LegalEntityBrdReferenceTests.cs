using System.Reflection;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Services.BusinessReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.Lookups;

// MOD-0220 — the LE wizard's Legal Form / Country / Base Currency lookups moved into governed BRD sets, read by
// the tenant actor through an allow-listed tenant-accessible endpoint. These guards pin: the seed content, the
// tenant-read scoping (İş3 boundary), and that the seeded sets and the endpoint allow-list stay in lock-step.
public sealed class LegalEntityBrdReferenceTests
{
    private static readonly string[] ExpectedSetCodes = ["legal-form", "country", "base-currency"];

    // ── Seed content ────────────────────────────────────────────────────────────────────────────

    private static JsonElement SeedRoot()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Seed", "business-reference-data", "legal-entity-reference.json");
        Assert.True(File.Exists(path), $"Seed file missing (not copied to output): {path}");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    [Fact]
    public void Seed_declares_the_three_global_active_reference_sets_with_expected_values()
    {
        var root = SeedRoot();
        Assert.Equal("BusinessReferenceData", root.GetProperty("module").GetString());

        var sets = root.GetProperty("sets").EnumerateArray().ToList();
        Assert.Equal(ExpectedSetCodes.OrderBy(x => x), sets.Select(s => s.GetProperty("set_code").GetString()!).OrderBy(x => x));

        foreach (var set in sets)
        {
            Assert.Equal("global", set.GetProperty("scope_type").GetString());
            Assert.Equal("Active", set.GetProperty("status").GetString());
            Assert.NotEmpty(set.GetProperty("values").EnumerateArray());
        }

        var valueCodes = ValueCodes(sets, "legal-form");
        Assert.Superset(new HashSet<string> { "CORPORATION", "LLC", "PARTNERSHIP", "SOLEPROP", "BRANCH", "REPOFFICE" }, valueCodes);

        Assert.Superset(new HashSet<string> { "TR", "US", "GB", "DE", "FR" }, ValueCodes(sets, "country"));
        Assert.Superset(new HashSet<string> { "USD", "EUR", "TRY" }, ValueCodes(sets, "base-currency"));
    }

    private static HashSet<string> ValueCodes(IEnumerable<JsonElement> sets, string setCode) =>
        sets.Single(s => s.GetProperty("set_code").GetString() == setCode)
            .GetProperty("values").EnumerateArray()
            .Select(v => v.GetProperty("value_code").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

    // ── Tenant-read scoping (endpoint) ──────────────────────────────────────────────────────────

    [Fact]
    public void Tenant_reference_data_endpoint_is_authenticated_but_not_platform_actor_only()
    {
        var authAttributes = typeof(TenantReferenceDataController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
        Assert.NotEmpty(authAttributes);
        Assert.DoesNotContain(authAttributes, a => string.Equals(a.Policy, "PlatformActor", StringComparison.Ordinal));
        Assert.Contains(authAttributes, a => string.IsNullOrEmpty(a.Policy));

        // Only the published-values read is exposed.
        var routes = typeof(TenantReferenceDataController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>())
            .Select(a => a.Template ?? string.Empty)
            .ToList();
        Assert.Equal(new[] { "sets/{setCode}/published-values" }, routes);
    }

    [Fact]
    public void Endpoint_allow_list_matches_the_seeded_sets_exactly()
    {
        // Every tenant-readable set must actually be seeded, and every seeded set must be readable — no drift.
        var allowList = (IEnumerable<string>)typeof(TenantReferenceDataController)
            .GetField("TenantReadableSets", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.Equal(ExpectedSetCodes.OrderBy(x => x), allowList.OrderBy(x => x, StringComparer.Ordinal));

        var seededCodes = SeedRoot().GetProperty("sets").EnumerateArray()
            .Select(s => s.GetProperty("set_code").GetString()!);
        Assert.Equal(allowList.OrderBy(x => x, StringComparer.Ordinal), seededCodes.OrderBy(x => x, StringComparer.Ordinal));
    }

    // ── Worker folder-scan ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Catalog_worker_scans_every_json_in_the_seed_directory()
    {
        var method = typeof(BusinessReferenceDataCatalogLoadWorker)
            .GetMethod("ResolveCatalogFiles", BindingFlags.NonPublic | BindingFlags.Static)!;

        var dir = Path.Combine(Path.GetTempPath(), "brd-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a-catalog.json");
            var b = Path.Combine(dir, "b-catalog.json");
            File.WriteAllText(a, "{}");
            File.WriteAllText(b, "{}");
            File.WriteAllText(Path.Combine(dir, "notes.txt"), "ignore me");

            // Passing ANY file in the directory returns EVERY *.json sibling (so a new seed file is auto-picked-up).
            var files = (List<string>)method.Invoke(null, [a])!;
            Assert.Equal(new[] { a, b }.OrderBy(x => x, StringComparer.Ordinal), files.OrderBy(x => x, StringComparer.Ordinal));
            Assert.DoesNotContain(files, f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
