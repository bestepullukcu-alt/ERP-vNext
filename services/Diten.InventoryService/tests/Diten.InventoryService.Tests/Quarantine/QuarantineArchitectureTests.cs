using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Persistence.Quarantine;
using Diten.InventoryService.Tests.Architecture;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 pack §10 / §16 Q1 / §17 architecture, on the compiled types AND the production source text:
/// <list type="number">
/// <item>decisions, decision events and history are INSERT-ONLY (K3 sabotage (a): an update method on the decision
/// repository → red);</item>
/// <item>0175 never writes a ledger collection and never calls the ledger repository's <c>PostAsync</c>: every posting goes
/// through <c>InternalMovementPostingService</c>; ledger collections are only READ, and only by the two read adapters;</item>
/// <item>no stock status or movement type outside the INVENTORY enums (K3 sabotage (b): <c>"SAMPLE"</c> in a 0175 mapping →
/// red).</item>
/// </list>
/// </summary>
public sealed class QuarantineArchitectureTests
{
    private static readonly Regex MutatingName = new(@"^(Update|Replace|Delete|Remove|Edit|Patch|Upsert|Modify|Set|Change|Purge|Drop)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static IReadOnlyList<SourceScan.SourceFile> QuarantineSources()
    {
        var files = SourceScan.Production().Where(f => f.RelativePath.Contains("/Quarantine/", StringComparison.Ordinal)
                                                       || f.RelativePath.EndsWith("QuarantineBlockedStockManifestProvider.cs", StringComparison.Ordinal)
                                                       || f.RelativePath.EndsWith("QuarantinePermissions.cs", StringComparison.Ordinal)).ToList();
        Assert.True(files.Count >= 20, $"only {files.Count} MOD-0175 sources found — the scan would be vacuous");
        return files;
    }

    [Fact]
    public void Decisions_are_insert_only_by_shape()
    {
        var decisionMembers = typeof(IQuarantineDecisionRepository).GetMethods().Select(m => m.Name).ToList();
        Assert.NotEmpty(decisionMembers);
        Assert.DoesNotContain(decisionMembers, n => MutatingName.IsMatch(n));
        Assert.All(decisionMembers, n => Assert.True(n.StartsWith("Find", StringComparison.Ordinal) || n.StartsWith("List", StringComparison.Ordinal)
                                                     || n.StartsWith("Events", StringComparison.Ordinal), $"{n} is not a read"));

        // the write session: decisions, events and history only ever get an Insert
        var sessionMembers = typeof(IQuarantineWriteSession).GetMethods().Select(m => m.Name).ToList();
        Assert.All(sessionMembers.Where(n => Regex.IsMatch(n, "Decision|History")),
            n => Assert.True(n.StartsWith("Insert", StringComparison.Ordinal) || n == nameof(IQuarantineWriteSession.DecisionEventsAsync), $"{n} touches an insert-only record"));
        Assert.Contains(nameof(IQuarantineWriteSession.InsertDecisionAsync), sessionMembers);

        var implementation = typeof(QuarantineDecisionRepository).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name);
        Assert.DoesNotContain(implementation, n => MutatingName.IsMatch(n));
    }

    [Fact]
    public void The_insert_only_collections_are_only_ever_read_or_inserted()
    {
        var repository = Assert.Single(QuarantineSources(), f => f.RelativePath.EndsWith("Persistence/Quarantine/QuarantineRepository.cs", StringComparison.Ordinal));
        foreach (var field in new[] { "_decisions", "_events", "_history" })
        {
            var calls = Regex.Matches(repository.Body, $@"{field}\s*\.\s*(\w+)").Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.NotEmpty(calls);
            Assert.All(calls, c => Assert.Contains(c, new[] { "Find", "InsertOneAsync", "InsertManyAsync" }));
        }
    }

    [Fact]
    public void MOD_0175_never_writes_a_ledger_collection_and_posts_only_through_the_internal_posting_service()
    {
        var sources = QuarantineSources();
        var readers = sources.Where(f => Regex.IsMatch(f.Body, @"InventoryCollections\.\w+")).Select(f => f.RelativePath).ToList();
        Assert.Equal(["Diten.InventoryService.Persistence/Quarantine/QuarantineLedgerReads.cs"], readers);

        var reads = Assert.Single(sources, f => f.RelativePath.EndsWith("QuarantineLedgerReads.cs", StringComparison.Ordinal));
        var ledgerCalls = Regex.Matches(reads.Body, @"_(transactions|positions|reservations)\s*\.\s*(\w+)").Select(m => m.Groups[2].Value).Distinct().ToList();
        Assert.NotEmpty(ledgerCalls);
        Assert.All(ledgerCalls, c => Assert.Equal("Find", c));

        // no 0175 file posts on the ledger repository; the poster is the one caller of the internal posting service
        Assert.All(sources, f => Assert.DoesNotMatch(@"\.PostAsync\(\s*(new\s+LedgerPosting|posting)", f.Body));
        var posters = sources.Where(f => f.Body.Contains("InternalMovementPostingService", StringComparison.Ordinal)).Select(f => f.RelativePath).ToList();
        Assert.Equal(["Diten.InventoryService.Application/Features/Quarantine/Services/QuarantineMovementPoster.cs"], posters);
        Assert.DoesNotContain(sources, f => f.Body.Contains("LedgerPosting(", StringComparison.Ordinal));
    }

    /// <summary>
    /// DEC-INV-08 / KQ-E: 0175 code uses no stock-status or movement-type vocabulary of its own. Every SCREAMING_SNAKE string
    /// literal in a 0175 source is either an INVENTORY wire value, a declared 0175 / INVENTORY constant (error code, source
    /// type, module code) or a permission-like key; anything else — <c>"SAMPLE"</c>, <c>"RELEASED"</c> — is a new status in
    /// disguise.
    /// </summary>
    [Fact]
    public void No_stock_status_or_movement_type_outside_the_INVENTORY_enums()
    {
        var inventoryWire = Enum.GetValues<StockStatus>().Select(WireNames.ToWire)
            .Concat(Enum.GetValues<MovementType>().Select(WireNames.ToWire)).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("QUARANTINE", inventoryWire);
        Assert.Equal(7, Enum.GetValues<StockStatus>().Length);
        Assert.Equal(15, Enum.GetValues<MovementType>().Length);

        var declared = new[] { typeof(QuarantineContract), typeof(QuarantineErrorCodes), typeof(QuarantineDecisionRules), typeof(InventoryErrorCodes) }
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!))
            .Concat(BlockReasonSeeds.All.Select(s => s.Code))
            .ToHashSet(StringComparer.Ordinal);

        // the manifest provider carries page / action / domain codes (registration vocabulary, pinned by QuarantineManifestTests)
        var literals = QuarantineSources().Where(f => !f.RelativePath.EndsWith("ManifestProvider.cs", StringComparison.Ordinal))
            .SelectMany(f => Regex.Matches(f.Body, @"""([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+|[A-Z]{4,})""").Select(m => (f.RelativePath, Value: m.Groups[1].Value)))
            .ToList();
        Assert.NotEmpty(literals);
        var unknown = literals.Where(l => !inventoryWire.Contains(l.Value) && !declared.Contains(l.Value)).Select(l => $"{l.RelativePath}: \"{l.Value}\"").Distinct().ToList();
        Assert.True(unknown.Count == 0, "0175 vocabulary outside INVENTORY / declared constants:\n" + string.Join("\n", unknown));

        // the stock-effect table maps only onto INVENTORY members
        Assert.All(QuarantineStockEffects.LineTarget.Values, v => Assert.True(Enum.IsDefined(v)));
        Assert.All(QuarantineStockEffects.DispositionMovement.Values, v => Assert.True(Enum.IsDefined(v)));
        Assert.Equal([StockStatus.Quarantine, StockStatus.QualityInspection], QuarantineStockEffects.HoldStatuses);

        // no type of the 0175 code is named after (or shadows) the INVENTORY vocabulary
        var quarantineTypes = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name?.StartsWith("Diten.InventoryService.", StringComparison.Ordinal) == true)
            .SelectMany(a => a.GetTypes()).Where(t => t.Namespace?.Contains(".Quarantine", StringComparison.Ordinal) == true).ToList();
        Assert.NotEmpty(quarantineTypes);
        Assert.DoesNotContain(quarantineTypes, t => t.Name is nameof(StockStatus) or nameof(MovementType));
    }
}

/// <summary>
/// The Q1 test approval port is a TEST device: enabled on a database that is not a DB-010 test database, the host refuses to
/// start (the controller itself is removed from the application model when the port is off — measured by
/// QuarantineValidationTests.Submit_without_an_approval_path_fails_closed_and_moves_nothing).
/// </summary>
public sealed class QuarantineTestApprovalGateTests
{
    private static Diten.InventoryService.Api.Quarantine.QuarantineTestApprovalGate Gate(bool enabled, string database)
        => new(Microsoft.Extensions.Options.Options.Create(new Diten.InventoryService.Application.Features.Quarantine.Ports.QuarantineTestApprovalOptions { Enabled = enabled }),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Mongo:DatabaseName"] = database }).Build());

    [Theory]
    [InlineData("diten_inventory")]
    [InlineData("diten_inventory_prod")]
    [InlineData("testing_inventory")]
    public async Task The_test_port_on_a_non_test_database_stops_the_host(string database)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Gate(true, database).StartAsync(CancellationToken.None));

    [Theory]
    [InlineData(true, "diten_inventory_s1_tests")]
    [InlineData(true, "diten_inventory_q1_itest")]
    [InlineData(false, "diten_inventory")]
    public async Task The_test_port_is_allowed_only_on_a_test_database_and_off_is_always_fine(bool enabled, string database)
        => await Gate(enabled, database).StartAsync(CancellationToken.None);
}
