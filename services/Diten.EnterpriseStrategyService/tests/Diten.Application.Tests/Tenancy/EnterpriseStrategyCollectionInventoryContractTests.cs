using System.Text.Json;
using Diten.Application.Common.Tenancy;
using Diten.Persistence.Evidence;
using static Diten.Application.Common.Tenancy.EnterpriseStrategyCollectionInventoryContracts;

namespace Diten.Application.Tests.Tenancy;

public sealed class EnterpriseStrategyCollectionInventoryContractTests
{
    private static readonly string[] EnvironmentNames =
    [
        MongoUriEnvironmentName,
        DatabaseEnvironmentName,
        TargetCopyEnvironmentName,
        RunEnvironmentName,
        AcknowledgementEnvironmentName
    ];

    [Theory]
    [MemberData(nameof(MissingEnvironmentCases))]
    public void Five_variable_gate_rejects_missing_values_before_reader_construction(string missingName)
    {
        var values = ValidEnvironment();
        values.Remove(missingName);
        var readerConstructionCount = 0;

        var error = Assert.Throws<InvalidOperationException>(() =>
        {
            var options = ReadOptions(name => values.GetValueOrDefault(name));
            readerConstructionCount++;
            _ = new ReadOnlyEnterpriseStrategyCollectionInventory(options);
        });

        Assert.Equal(0, readerConstructionCount);
        Assert.Contains(missingName, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("mongodb://", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_is_exact_and_does_not_normalize_acknowledgement_or_identifiers()
    {
        var values = ValidEnvironment();
        values[AcknowledgementEnvironmentName] = RequiredAcknowledgement.ToLowerInvariant();
        Assert.Throws<InvalidOperationException>(() => ReadOptions(values.GetValueOrDefault));

        values = ValidEnvironment();
        values[TargetCopyEnvironmentName] = "target copy";
        Assert.Throws<InvalidOperationException>(() => ReadOptions(values.GetValueOrDefault));
    }

    [Fact]
    public void Database_identity_must_equal_the_acknowledged_target_copy()
    {
        var values = ValidEnvironment();
        values[DatabaseEnvironmentName] = "different-copy";
        Assert.Throws<InvalidOperationException>(() => ReadOptions(values.GetValueOrDefault));
    }

    [Theory]
    [InlineData("https://host/database")]
    [InlineData("not-a-uri")]
    public void Gate_rejects_non_mongodb_uris_without_disclosing_values(string mongoUri)
    {
        var values = ValidEnvironment();
        values[MongoUriEnvironmentName] = mongoUri;
        var error = Assert.Throws<InvalidOperationException>(() => ReadOptions(values.GetValueOrDefault));
        Assert.DoesNotContain(mongoUri, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Valid_gate_returns_exact_values()
    {
        var values = ValidEnvironment();
        var options = ReadOptions(values.GetValueOrDefault);
        Assert.Equal(values[DatabaseEnvironmentName], options.Database);
        Assert.Equal(values[TargetCopyEnvironmentName], options.TargetCopyId);
        Assert.Equal(values[RunEnvironmentName], options.RunId);
    }

    [Fact]
    public void Public_inventory_commands_are_exactly_the_four_read_operations()
    {
        Assert.Equal(
            ["aggregate", "count", "listCollections", "listIndexes"],
            ReadOnlyEnterpriseStrategyCollectionInventory.InventoryCommandNames.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["authenticate", "buildInfo", "endSessions", "getMore", "getParameter", "getnonce", "hello", "isMaster", "killCursors", "saslContinue", "saslStart"],
            ReadOnlyEnterpriseStrategyCollectionInventory.DriverSupportCommandNames.Order(StringComparer.Ordinal));
        var allCommands = ReadOnlyEnterpriseStrategyCollectionInventory.InventoryCommandNames
            .Concat(ReadOnlyEnterpriseStrategyCollectionInventory.DriverSupportCommandNames);
        Assert.DoesNotContain(allCommands,
            name => name.Contains("insert", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("update", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("create", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("drop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evidence_contract_is_sanitized_deterministic_and_contains_no_connection_metadata()
    {
        var collection = new CollectionEvidence(
            "goals|unsafe\nname",
            3,
            "unreviewed",
            [new IndexEvidence("tenant`index", true, false, null, [new IndexKeyEvidence("TenantId", "1")])],
            [new FieldTypeEvidence("TenantId", "binData", 3)]);
        var digest = ComputeMetadataDigest([collection]);
        var report = new InventoryReport("copy-1", "run-1", DateTimeOffset.UnixEpoch, [collection], digest);
        var json = JsonSerializer.Serialize(report);

        Assert.Equal(digest, ComputeMetadataDigest([collection]));
        Assert.Equal("goals\\|unsafename", SanitizeMarkdown(collection.Name));
        Assert.DoesNotContain("MongoUri", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Database", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inventory_tool_is_offline_by_default_and_outside_runtime_composition()
    {
        var root = FindRepositoryRoot();
        var solution = File.ReadAllText(Path.Combine(root, "services/Diten.EnterpriseStrategyService/Diten.EnterpriseStrategy.sln"));
        var reader = File.ReadAllText(Path.Combine(root,
            "services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Persistence/Evidence/ReadOnlyEnterpriseStrategyCollectionInventory.cs"));
        var program = File.ReadAllText(Path.Combine(root,
            "services/Diten.EnterpriseStrategyService/tools/Diten.EnterpriseStrategy.ReadOnlyInventory/Program.cs"));

        Assert.DoesNotContain("ReadOnlyInventory", solution, StringComparison.Ordinal);
        Assert.Contains("ReadOptions(Environment.GetEnvironmentVariable)", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService", program, StringComparison.Ordinal);
        Assert.DoesNotContain(string.Concat("Ensure", "IndexesAsync"), reader, StringComparison.Ordinal);
        Assert.DoesNotContain(string.Concat("Ensure", "AppliedAsync"), reader, StringComparison.Ordinal);
        Assert.DoesNotContain("DbInitializer", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("StartSession", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("WithTransaction", reader, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_evidence_is_absent_until_a_manual_target_copy_run()
    {
        var evidence = Path.Combine(FindRepositoryRoot(), "docs/audits/enterprise-strategy-security-tenancy-migration");
        Assert.False(File.Exists(Path.Combine(evidence, "collection-inventory-v1.json")));
        Assert.False(File.Exists(Path.Combine(evidence, "collection-inventory-v1.md")));
    }

    public static IEnumerable<object[]> MissingEnvironmentCases() =>
        EnvironmentNames.Select(name => new object[] { name });

    private static Dictionary<string, string> ValidEnvironment() => new(StringComparer.Ordinal)
    {
        [MongoUriEnvironmentName] = "mongodb://inventory.invalid:27099",
        [DatabaseEnvironmentName] = "esbp-target-copy-20260829",
        [TargetCopyEnvironmentName] = "esbp-target-copy-20260829",
        [RunEnvironmentName] = "run-20260829-01",
        [AcknowledgementEnvironmentName] = RequiredAcknowledgement
    };

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                return current.FullName;
        throw new InvalidOperationException("Repository root was not found.");
    }
}
