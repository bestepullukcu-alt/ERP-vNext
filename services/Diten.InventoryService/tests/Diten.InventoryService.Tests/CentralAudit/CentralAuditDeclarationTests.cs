using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Api.Expiry;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Api.Recall;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Tests.TestInfrastructure;
using MediatR;
using Xunit;

namespace Diten.InventoryService.Tests.CentralAudit;

/// <summary>
/// BL-501 — measured on the production assemblies and sources:
/// every Inventory write command declares its central record, in values Platform knows, under a module code the service
/// registers; and every Mongo transaction a command can run in enlists the event (the service runs with
/// RequireEnlistment, so a transaction that forgot to enlist would drop records without a word).
/// </summary>
public sealed class CentralAuditDeclarationTests
{
    /// <summary>The write commands, by the architecture test's definition (IRequest, not *Query, not .Queries).</summary>
    private static readonly Type[] WriteCommands = typeof(InventoryCentralAudit).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false }
                    && t.GetInterfaces().Any(i => i == typeof(IBaseRequest))
                    && !(t.Name.EndsWith("Query", StringComparison.Ordinal) || (t.Namespace ?? string.Empty).Contains(".Queries", StringComparison.Ordinal)))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void Every_write_command_declares_its_central_audit_record()
    {
        // 58 measured 2026-10-14 (AuditTrailStandardTests inventory row); 59 after the MVP-1 merge (R4 PrepareRecallSettingsChangeCommand,
        // CT HOLD 2026-10-14). A new command fails here AND in the architecture test.
        // 66 after WP-0177-R5 (7 drill commands, CT decision 2026-10-14). 67 after WP-0177-R7b (CreateRecallEvidenceExportCommand).
        Assert.Equal(67, WriteCommands.Length);
        Assert.Empty(WriteCommands.Where(t => !typeof(ICentralAuditDeclaration).IsAssignableFrom(t)).Select(t => t.Name));
    }

    [Fact]
    public void Category_and_operation_are_platform_enum_names_and_the_source_is_a_registered_module()
    {
        var modules = new IModuleManifestProvider[]
        {
            new InventoryLedgerManifestProvider(),
            new LotSerialTrackingManifestProvider(),
            new QuarantineBlockedStockManifestProvider(),
            new ExpiryFefoManifestProvider(),
            new RecallReadinessManifestProvider()
        }.Select(p => p.GetManifest().ModuleCode).ToHashSet(StringComparer.Ordinal);

        foreach (var type in WriteCommands)
        {
            var metadata = ((ICentralAuditDeclaration)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type)).CentralAudit;
            Assert.True(CentralAuditPlatformValues.IsKnown(metadata), $"{type.Name}: {metadata.Category} / {metadata.Operation} is not a Platform audit enum name");
            Assert.NotEqual("Unknown", metadata.Operation);
            Assert.Contains(metadata.SourceModule, modules);
            Assert.False(string.IsNullOrWhiteSpace(metadata.EntityType), type.Name);
        }
    }

    [Fact]
    public void Only_the_command_that_writes_nothing_here_is_recorded_outside_a_transaction()
    {
        var outside = WriteCommands.Where(t => typeof(ICentralAuditOutsideTransaction).IsAssignableFrom(t)).Select(t => t.Name);

        Assert.Equal(["PrepareWorkflowTemplateCommand"], outside);
    }

    /// <summary>
    /// Every <c>WithTransactionAsync(</c> callback in Persistence enlists the event — except the four background sweepers,
    /// which run outside any command (no marked command in flight: an enlistment there is a no-op by design).
    /// </summary>
    [Fact]
    public void Every_transaction_a_command_can_run_in_enlists_the_central_audit_event()
    {
        var sweepers = new[]
        {
            "RetestHoldSweeperHostedService.cs", "ReservationExpirySweeper.cs", "SkuPositionRebuilder.cs", "LotExpirySweeper.cs"
        };
        var persistence = Path.Combine(RepoPaths.Root, "services", "Diten.InventoryService", "src", "Diten.InventoryService.Persistence");
        var failures = new List<string>();
        var seen = 0;

        foreach (var file in Directory.GetFiles(persistence, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var text = Regex.Replace(File.ReadAllText(file), @"//[^\n]*", string.Empty);
            if (sweepers.Contains(Path.GetFileName(file)))
            {
                Assert.DoesNotContain("CentralAuditTransaction.EnlistAsync(", text, StringComparison.Ordinal);
                continue;
            }

            Assert.DoesNotContain("StartTransaction(", text, StringComparison.Ordinal);
            var starts = Regex.Matches(text, @"WithTransactionAsync\(").Select(m => m.Index).ToList();
            for (var i = 0; i < starts.Count; i++)
            {
                seen++;
                var end = i + 1 < starts.Count ? starts[i + 1] : text.Length;
                var window = text[starts[i]..Math.Min(end, starts[i] + 1200)];
                if (!window.Contains("CentralAuditTransaction.EnlistAsync(s", StringComparison.Ordinal))
                {
                    failures.Add($"{Path.GetFileName(file)}: a WithTransactionAsync callback does not enlist the central-audit event");
                }
            }
        }

        Assert.Equal(10, seen); // 9 + RecallDrillRepository.InTransactionAsync (WP-0177-R5)
        Assert.Empty(failures);
    }
}
