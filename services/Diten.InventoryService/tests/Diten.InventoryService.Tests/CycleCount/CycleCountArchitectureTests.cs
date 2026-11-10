using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers.CycleCounting;
using Diten.InventoryService.Api.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.Architecture;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-1 — measured on the production assemblies and sources: no second balance (AC-3), the keys the controller enforces = the manifest
/// (AC-7), GET / POST only (AC-51, DL-1 part) and the localization key names (SB3/F-2).
/// </summary>
public sealed class CycleCountArchitectureTests
{
    private static readonly string[] CycleCountFolders = ["/CycleCount/", "/CycleCounts/", "/CycleCounting/"];

    private static IReadOnlyList<SourceScan.SourceFile> CycleCountSources()
        => SourceScan.Production().Where(f => CycleCountFolders.Any(d => ("/" + f.RelativePath).Contains(d, StringComparison.Ordinal))).ToList();

    // ── AC-3 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC3_no_cycle_count_type_carries_a_stock_total()
    {
        var types = typeof(CycleCount).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Diten.InventoryService.Domain.CycleCounting", StringComparison.Ordinal) == true && typeof(EntityBase).IsAssignableFrom(t))
            .ToList();
        Assert.Equal(["CycleCount", "CycleCountBinClaim", "CycleCountEvent", "CycleCountLine"], types.Select(t => t.Name).Order(StringComparer.Ordinal));
        var carriers = types.Where(t => t.GetProperties().Any(p => p.Name is "OnHand" or "Available" or "Reserved")).Select(t => t.Name).ToList();
        Assert.True(carriers.Count == 0, "a MOD-0181 type carries a stock total: " + string.Join(", ", carriers));
    }

    [Fact]
    public void AC3_cycle_count_code_names_no_ledger_repository_and_no_ledger_collection()
    {
        var files = CycleCountSources();
        Assert.True(files.Count >= 25, $"vacuous scan: {files.Count} MOD-0181 files");
        var forbidden = new[] { "IInventoryLedgerRepository", "InventoryLedgerRepository", "inventory_transactions", "inventory_balances" };
        var hits = files.SelectMany(f => forbidden.Where(w => f.Body.Contains(w, StringComparison.Ordinal)).Select(w => $"{f.RelativePath}: {w}")).ToList();
        Assert.True(hits.Count == 0, "MOD-0181 reaches the ledger past its doors:\n" + string.Join("\n", hits));
    }

    /// <summary>The ledger's collections are only ever READ by MOD-0181 — <c>Find</c>, never a write (pack §2.3 rule 2).</summary>
    [Fact]
    public void AC3_the_ledger_collections_are_only_read_by_cycle_count_persistence()
    {
        var repository = Assert.Single(CycleCountSources(), f => f.RelativePath.EndsWith("/CycleCountRepository.cs", StringComparison.Ordinal));
        var ledgerCalls = Regex.Matches(repository.Body, @"GetCollection<(InventoryBalance|InventoryTransaction|Location)>\(InventoryCollections\.\w+\)\s*\.\s*(\w+)")
            .Select(m => m.Groups[2].Value).Distinct().ToList();
        Assert.True(Regex.Matches(repository.Body, @"GetCollection<(InventoryBalance|InventoryTransaction|Location)>").Count >= 3);
        Assert.Equal(Regex.Matches(repository.Body, @"GetCollection<(InventoryBalance|InventoryTransaction|Location)>").Count,
            Regex.Matches(repository.Body, @"GetCollection<(InventoryBalance|InventoryTransaction|Location)>\(InventoryCollections\.\w+\)\s*\.\s*Find\(").Count);
        Assert.Equal(["Find"], ledgerCalls);
    }

    // ── AC-7 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC7_the_manifest_declares_exactly_the_cycle_count_keys_the_controller_enforces()
    {
        var manifest = new CycleCountingManifestProvider().GetManifest();
        Assert.Equal("CYCLE-COUNTING", manifest.ModuleCode);
        Assert.Equal(CycleCountCentralAudit.Module, manifest.ModuleCode);
        Assert.Equal(181, manifest.SortOrder);
        Assert.Empty(manifest.Pages); // DL-1: no page yet (the screens come with DL-4 / DL-5)

        var declared = (manifest.ApiPermissions ?? []).Select(p => p.PermissionKey).ToList();
        Assert.All(manifest.ApiPermissions ?? [], p => Assert.Equal("Tenant", p.Scope));
        Assert.All(declared, k => Assert.StartsWith("cycle-count.", k, StringComparison.Ordinal));
        Assert.Equal(CycleCountPermissions.Implemented.Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(declared, k => k.EndsWith(".delete", StringComparison.Ordinal)); // G-0181-01: no delete key

        // every controller of the namespace the manifest completeness guard excludes (CT DÜŞÜK-2): the DL-3 / DL-5 controllers join it here
        Assert.Contains(typeof(CycleCountsController), Controllers());
        var enforced = Actions().Select(m => (Action: $"{m.DeclaringType!.Name}.{m.Name}", Key: m.GetCustomAttribute<HasPermissionAttribute>()?.Policy?.Replace("Permission:", string.Empty))).ToList();
        Assert.NotEmpty(enforced);
        Assert.Empty(enforced.Where(e => e.Key is null).Select(e => e.Action)); // an action without [HasPermission] is named here
        Assert.Equal(declared.Order(StringComparer.Ordinal), enforced.Select(e => e.Key!).Distinct().Order(StringComparer.Ordinal));
    }

    // ── AC-51 (DL-1 part) ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC51_the_cycle_count_controller_opens_GET_and_POST_only_under_api_warehouse()
    {
        Assert.All(Controllers(), c => Assert.Equal("api/warehouse", c.GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>()?.Template));
        var verbs = Actions().SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>()).SelectMany(h => h.HttpMethods).Distinct().Order().ToList();
        Assert.Equal(["GET", "POST"], verbs);
        var routes = Actions().SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>().Select(h => $"{h.HttpMethods.Single()} {h.Template}")).Order(StringComparer.Ordinal);
        Assert.Equal(
            [
                "GET cycle-counts", "GET cycle-counts/{countId}", "GET cycle-counts/{countId}/events", "GET cycle-counts/{countId}/lines",
                "POST cycle-counts", "POST cycle-counts/bulk-delete", "POST cycle-counts/{countId}/cancel", "POST cycle-counts/{countId}/lines",
                "POST cycle-counts/{countId}/lines/{lineId}/count", "POST cycle-counts/{countId}/release", "POST cycle-counts/{countId}/submit",
                "POST cycle-counts/{countId}/update"
            ],
            routes);
    }

    /// <summary>
    /// Every controller in <c>….Controllers.CycleCounting</c> — the namespace <c>InventoryLedgerManifestCompletenessTests</c> excludes, so AC-7 is
    /// the only guard of its keys and must see all of it, not one type.
    /// </summary>
    private static IReadOnlyList<Type> Controllers()
        => typeof(CycleCountsController).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t)
                        && t.Namespace?.EndsWith(".Controllers.CycleCounting", StringComparison.Ordinal) == true)
            .ToList();

    private static IEnumerable<MethodInfo> Actions()
        => Controllers().SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    // ── SB3/F-2 ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Final look 03 F-2: the count status and the line status have SEPARATE key prefixes, each with the Web family that owns its values; the
    /// key names are fixed here (DL-4 / DL-5 parity guard measures the values). Every enum value has its key.
    /// </summary>
    [Fact]
    public void SB3_F2_status_and_line_status_keys_are_separate_and_name_their_web_family()
    {
        Assert.Equal("CycleCount.Status.", CycleCountTextKeys.StatusPrefix);
        Assert.Equal("CycleCount.LineStatus.", CycleCountTextKeys.LineStatusPrefix);
        Assert.Equal("CycleCount.Decision.", CycleCountTextKeys.DecisionPrefix);
        Assert.Equal("CycleCount.Decision.Source.", CycleCountTextKeys.DecisionSourcePrefix);
        Assert.Equal(4, CycleCountTextKeys.WebFamilies.Count);
        Assert.Equal("Views/Warehouse/CycleCounts/CycleCountsIndex", CycleCountTextKeys.WebFamilies[CycleCountTextKeys.StatusPrefix]);
        Assert.Equal("Views/Warehouse/CountVariances/CountVariancesIndex", CycleCountTextKeys.WebFamilies[CycleCountTextKeys.LineStatusPrefix]);

        var statusKeys = Enum.GetValues<CycleCountStatus>().Select(CycleCountTextKeys.Status).ToList();
        var lineKeys = Enum.GetValues<CycleCountLineStatus>().Select(CycleCountTextKeys.LineStatus).ToList();
        Assert.Contains("CycleCount.Status.Released", statusKeys);
        Assert.Contains("CycleCount.LineStatus.Open", lineKeys);
        Assert.Empty(statusKeys.Intersect(lineKeys)); // no key is shared between the two families
    }
}
