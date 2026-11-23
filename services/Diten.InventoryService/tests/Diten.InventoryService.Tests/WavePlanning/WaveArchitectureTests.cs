using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Warehouse;
using Diten.InventoryService.Api.WavePlanning;
using Diten.InventoryService.Application.Features.WavePlanning;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.Architecture;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using static Diten.InventoryService.Tests.WavePlanning.WaveScenario;

namespace Diten.InventoryService.Tests.WavePlanning;

/// <summary>
/// MOD-0180 DL-1 — measured on the production assemblies and sources: the draft and the controller name the same operations (AC-1), the keys
/// the controllers enforce = the manifest and the sub-namespace has one owner (AC-5), GET / POST only (AC-8), the maintenance service creates no
/// wave (AC-12) and MOD-0178's data is only read (AC-16).
/// </summary>
public sealed class WaveArchitectureTests(WaveApiFactory factory) : IClassFixture<WaveApiFactory>
{
    private static readonly string[] WaveFolders = ["/WavePlanning/", "/Waves/", "/WaveExceptions/", "/WaveKpis/"];

    private static IReadOnlyList<SourceScan.SourceFile> WaveSources()
        => SourceScan.Production().Where(f => WaveFolders.Any(d => ("/" + f.RelativePath.Replace('\\', '/')).Contains(d, StringComparison.Ordinal))).ToList();

    private static IEnumerable<Type> WaveControllers()
        => typeof(WavePlanningManifestProvider).Assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t) && !t.IsAbstract
                        && t.Namespace?.EndsWith(".Controllers.WavePlanning", StringComparison.Ordinal) == true);

    private static IEnumerable<MethodInfo> Actions()
        => WaveControllers().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static string Key(MethodInfo action) => action.GetCustomAttribute<HasPermissionAttribute>()?.Policy?.Replace("Permission:", string.Empty) ?? string.Empty;

    private static string Route(HttpMethodAttribute http) => $"{http.HttpMethods.Single()} /{Regex.Replace(http.Template!, @":guid\}", "}")}";

    // ── AC-1 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC1_the_draft_names_23_operations_and_its_DL1_table_is_the_controllers_routes_and_keys()
    {
        var rows = WaveDraftContract.DraftRows();
        Assert.Equal(23, rows.Count);
        Assert.Equal(23, rows.Select(r => r.OperationId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(23, rows.Select(r => r.Method + " " + r.Path).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows, r => Assert.StartsWith(WavePermissions.Prefix, r.Permission, StringComparison.Ordinal));
        Assert.All(rows, r => Assert.Contains(r.Method, new[] { "GET", "POST" })); // no PUT / DELETE anywhere in the bundle
        Assert.Contains("1.0.0-draft", File.ReadAllText(Path.Combine(TestInfrastructure.RepoPaths.Root, WaveDraftContract.DraftPath.Replace('/', Path.DirectorySeparatorChar))));

        var binding = rows.Where(r => r.Dl1).Select(r => $"{r.Method} {r.Path} {r.Permission}").Order(StringComparer.Ordinal).ToList();
        var served = Actions().SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>().Select(h => $"{Route(h)} {Key(a)}")).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(14, binding.Count);
        Assert.Equal(binding, served); // the draft's binding table = what the service serves (route, verb, key)

        // the test's own operation table (what the contract handler validates) is the draft's DL-1 table too
        Assert.Equal(rows.Where(r => r.Dl1).Select(r => $"{r.Method} {r.Path} {r.OperationId}").Order(StringComparer.Ordinal),
            WaveDraftContract.Operations.Select(o => $"{o.Method} /{o.Template} {o.OperationId}").Order(StringComparer.Ordinal));
    }

    /// <summary>Every DL-1 operation answers in the draft's shape — one walk through all 14, each validated by the contract handler.</summary>
    [Fact]
    public async Task AC1_every_DL1_operation_answers_in_the_draft_shape()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var outbound = s.SeedOutbound();
        var wave = await s.CreatedAsync(s.Form(shipToFilter: "Ankara", maxOrders: 5));
        wave = await Expect(s.UpdateAsync(Id(wave), s.Form(note: "sozlesme"), ETag(wave)), 200);
        await Expect(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}"), 200);
        await Expect(s.GetAsync($"/api/warehouse/waves/candidates?warehouseId={s.Warehouse:D}"), 200);
        await Expect(s.GetAsync($"/api/warehouse/waves/{Id(wave)}/suggestions"), 200);
        wave = await s.AddedAsync(wave, outbound);
        await Expect(s.GetAsync($"/api/warehouse/waves/{Id(wave)}"), 200);
        await Expect(s.GetAsync($"/api/warehouse/waves/{Id(wave)}/orders"), 200);
        await Expect(s.GetAsync($"/api/warehouse/waves/{Id(wave)}/load"), 200);
        wave = (await Expect(s.RemoveAsync(Id(wave), [outbound], ETag(wave)), 200)).GetProperty("wave");
        await Expect(s.CancelAsync(Id(wave), ETag(wave)), 200);
        await Expect(s.GetAsync($"/api/warehouse/waves/{Id(wave)}/events"), 200);
        await Expect(s.GetSettingsAsync(), 200);

        foreach (var operation in WaveDraftContract.Operations)
        {
            Assert.True(WaveDraftContract.Validated.Keys.Any(k => k.StartsWith(operation.OperationId + " 20", StringComparison.Ordinal)),
                $"{operation.OperationId} was never validated against the draft");
        }
    }

    // ── AC-5 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC5_the_manifest_declares_exactly_the_wave_keys_the_controllers_enforce()
    {
        var manifest = new WavePlanningManifestProvider().GetManifest();
        Assert.Equal("WAVE-PLANNING", manifest.ModuleCode);
        Assert.Equal(WaveCentralAudit.Module, manifest.ModuleCode);
        Assert.Equal(180, manifest.SortOrder);
        Assert.Empty(manifest.Pages); // DL-1: no page yet (the screens come with DL-4 / DL-5)

        var declared = (manifest.ApiPermissions ?? []).Select(p => p.PermissionKey).ToList();
        Assert.All(manifest.ApiPermissions ?? [], p => Assert.Equal("Tenant", p.Scope));
        Assert.Equal(WavePermissions.Implemented.Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(declared, k => k.EndsWith(".delete", StringComparison.Ordinal)); // G-0180-01: no delete key

        Assert.NotEmpty(WaveControllers());
        Assert.All(WaveControllers(), t => Assert.Equal("Diten.InventoryService.Api.Controllers.WavePlanning", t.Namespace));
        var enforced = Actions().Select(Key).ToList();
        Assert.Equal(14, enforced.Count);
        Assert.All(enforced, k => Assert.NotEqual(string.Empty, k)); // every action carries [HasPermission]
        Assert.Equal(declared.Order(StringComparer.Ordinal), enforced.Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The sub-namespace has ONE owner (pack §14, X-16; S-5c / S-5d): no key of MOD-0178's manifest — page, action or API permission — starts
    /// with <c>warehouse.waves.</c>, and every key of MOD-0180's manifest does. Read from the PRODUCTION manifests.
    /// </summary>
    [Fact]
    public void AC5_the_warehouse_waves_sub_namespace_is_MOD_0180s_alone()
    {
        static List<string> Keys(Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument manifest)
            => manifest.Pages.Select(p => p.RequiredPermission)
                .Concat(manifest.Pages.SelectMany(p => p.Actions ?? []).Select(a => a.PermissionKey))
                .Concat((manifest.ApiPermissions ?? []).Select(p => p.PermissionKey))
                .Where(k => !string.IsNullOrEmpty(k)).Select(k => k!).ToList();

        var warehouse = Keys(new PutawayPickingPackingManifestProvider().GetManifest());
        Assert.True(warehouse.Count >= 8, $"vacuous scan: {warehouse.Count} MOD-0178 keys");
        Assert.DoesNotContain(warehouse, k => k.StartsWith(WavePermissions.Prefix, StringComparison.Ordinal));

        var waves = Keys(new WavePlanningManifestProvider().GetManifest());
        Assert.True(waves.Count >= 5, $"vacuous scan: {waves.Count} MOD-0180 keys");
        Assert.All(waves, k => Assert.StartsWith(WavePermissions.Prefix, k, StringComparison.Ordinal));
    }

    // ── AC-8 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC8_the_wave_controllers_open_GET_and_POST_only_under_api_warehouse()
    {
        Assert.All(WaveControllers(), t => Assert.Equal("api/warehouse", t.GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>()?.Template));
        var verbs = Actions().SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>()).SelectMany(h => h.HttpMethods).Distinct().Order().ToList();
        Assert.Equal(["GET", "POST"], verbs);
        var routes = Actions().SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>().Select(h => $"{h.HttpMethods.Single()} {h.Template}")).Order(StringComparer.Ordinal);
        Assert.Equal(
            [
                "GET waves", "GET waves/candidates", "GET waves/settings/{warehouseId}", "GET waves/{waveId:guid}", "GET waves/{waveId:guid}/events",
                "GET waves/{waveId:guid}/load", "GET waves/{waveId:guid}/orders", "GET waves/{waveId:guid}/suggestions", "POST waves",
                "POST waves/settings/{warehouseId}/update", "POST waves/{waveId:guid}/cancel", "POST waves/{waveId:guid}/orders",
                "POST waves/{waveId:guid}/orders/remove", "POST waves/{waveId:guid}/update"
            ],
            routes);
    }

    // ── AC-12 ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// No periodic wave building (D-1; S-12b): the commands the maintenance service names are a subset of the three it may send, it never names
    /// a create / edit command, opens no transaction and touches no collection.
    /// </summary>
    [Fact]
    public void AC12_the_maintenance_service_sends_only_its_three_commands_and_never_creates_a_wave()
    {
        var service = Assert.Single(WaveSources(), f => f.RelativePath.Replace('\\', '/').EndsWith("/WaveMaintenanceHostedService.cs", StringComparison.Ordinal));
        var code = SourceScan.WithoutComments(service.Body);
        var allowed = new[] { "CompleteWaveReleaseCommand", "ReconcileWaveCommand", "ReleasePendingOrdersCommand" };

        var named = Regex.Matches(code, @"\b(\w+Command)\b").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(named.All(allowed.Contains), "the maintenance service names a command it may not send: " + string.Join(", ", named.Except(allowed)));
        Assert.Contains("IMediator", code, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "WithTransactionAsync", "StartTransaction", "GetCollection", "IWaveRepository", "InsertWaveAsync" })
        {
            Assert.DoesNotContain(forbidden, code, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AC12_a_maintenance_round_sends_nothing_in_DL1_and_creates_no_wave()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        s.SeedOutbound();
        var service = (Diten.InventoryService.Persistence.WavePlanning.WaveMaintenanceHostedService)factory.Services.GetService(typeof(Diten.InventoryService.Persistence.WavePlanning.WaveMaintenanceHostedService))!;

        Assert.Equal(0, await service.RunOnceAsync());

        Assert.Empty(s.Stored(Diten.InventoryService.Persistence.WavePlanning.WaveCollections.Waves));
        Assert.Empty(s.Stored(Diten.InventoryService.Persistence.WavePlanning.WaveCollections.Orders));
    }

    // ── AC-16 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC16_no_wave_type_carries_a_stock_total()
    {
        var types = typeof(Wave).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Diten.InventoryService.Domain.WavePlanning", StringComparison.Ordinal) == true && typeof(EntityBase).IsAssignableFrom(t))
            .ToList();
        Assert.Equal(["Wave", "WaveEvent", "WaveOrder", "WaveOutboundDocument", "WaveWarehouseSettings"], types.Select(t => t.Name).Order(StringComparer.Ordinal));
        var all = typeof(Wave).Assembly.GetTypes().Where(t => t.Namespace?.StartsWith("Diten.InventoryService.Domain.WavePlanning", StringComparison.Ordinal) == true);
        var carriers = all.Where(t => t.GetProperties().Any(p => p.Name is "OnHand" or "Available" or "Reserved")).Select(t => t.Name).ToList();
        Assert.True(carriers.Count == 0, "a MOD-0180 type carries a stock total: " + string.Join(", ", carriers));
    }

    /// <summary>
    /// MOD-0178's data is only READ (pack §2.3 rule 2; S-16a / S-16b): the wave code names none of MOD-0178's repositories, reaches its
    /// collections only through <c>WarehouseCollections.*</c> — never a literal name — and calls nothing but <c>Find</c> on them.
    /// </summary>
    [Fact]
    public void AC16_wave_code_reads_MOD_0178s_collections_with_Find_only_and_names_none_of_its_repositories()
    {
        var files = WaveSources();
        Assert.True(files.Count >= 40, $"vacuous scan: {files.Count} MOD-0180 files");

        var repositories = new[] { "IOutboundShipmentRepository", "OutboundShipmentRepository", "IWarehouseTaskRepository", "WarehouseTaskRepository",
            "IWarehouseLocationRoleRepository", "IInventoryLedgerRepository" };
        var named = files.SelectMany(f => repositories.Where(r => Regex.IsMatch(SourceScan.WithoutComments(f.Body), $@"\b{r}\b")).Select(r => $"{f.RelativePath}: {r}")).ToList();
        Assert.True(named.Count == 0, "MOD-0180 names a repository of another module:\n" + string.Join("\n", named));

        var literals = files.SelectMany(f => Regex.Matches(SourceScan.WithoutComments(f.Body), "\"(wms_(?!wave_)[a-z_]+|inventory_[a-z_]+)\"")
            .Select(m => $"{f.RelativePath}: {m.Groups[1].Value}")).ToList();
        Assert.True(literals.Count == 0, "MOD-0180 names another module's collection by a literal:\n" + string.Join("\n", literals));

        var touching = files.Where(f => f.Body.Contains("WarehouseCollections.", StringComparison.Ordinal)).ToList();
        var reader = Assert.Single(touching); // only the read-only view of MOD-0178's outbounds
        Assert.EndsWith("/WaveInventoryReader.cs", reader.RelativePath.Replace('\\', '/'), StringComparison.Ordinal);
        var code = SourceScan.WithoutComments(reader.Body);

        // no call chained straight onto a MOD-0178 collection …
        Assert.Empty(Regex.Matches(code, @"\(WarehouseCollections\.\w+\)\s*\.\s*(\w+)").Select(m => m.Groups[1].Value));
        // … and every field bound to one is only ever asked to Find
        var fields = Regex.Matches(code, @"(_\w+)\s*=\s*database\.GetCollection<\w+>\(WarehouseCollections\.\w+\)").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(Regex.Matches(code, @"WarehouseCollections\.\w+").Count, fields.Count);
        Assert.NotEmpty(fields);
        foreach (var field in fields)
        {
            var calls = Regex.Matches(code, $@"{Regex.Escape(field)}\s*\.\s*(\w+)").Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.Equal(["Find"], calls);
        }

        // the wave store itself writes only its own collections
        var store = Assert.Single(files, f => f.RelativePath.Replace('\\', '/').EndsWith("/WaveRepository.cs", StringComparison.Ordinal));
        Assert.Equal(["WaveCollections.Events", "WaveCollections.Orders", "WaveCollections.Settings", "WaveCollections.Waves"],
            Regex.Matches(SourceScan.WithoutComments(store.Body), @"GetCollection<\w+>\((\w+\.\w+)\)").Select(m => m.Groups[1].Value).Order(StringComparer.Ordinal));
    }
}
