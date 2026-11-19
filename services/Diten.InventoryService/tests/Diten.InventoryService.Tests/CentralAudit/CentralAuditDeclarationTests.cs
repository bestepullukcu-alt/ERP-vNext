using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Api.Expiry;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Api.Recall;
using Diten.InventoryService.Api.Warehouse;
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
        // 71 after MOD-0178 DL-1 (the 4 location-role commands, pack §5 fifth seam); 82 after DL-2 (the 11 task commands: create, update,
        // claim, bulk assign — Q-13 A —, confirm, retry, close short, cancel, returned, resolve, reconcile).
        // 84 after MOD-0175-FU01 DL-1 (ApplySourceReversalToHoldCommand, ApplySourceReversalToBlockedCommand; measured 82 on the branch at 25e06fe08).
        // 87 after MOD-0178 DL-4 (pack §5 rule "82 + the slice's S2 write commands": CreateOutboundShipment, UpdateOutboundShipment,
        // CompleteOutboundAllocation — 0178's count is 85; the 2 MOD-0175-FU01 commands above are outside that rule; measured 84 at d6aff589e).
        // 101 after MOD-0178 DL-5 (the 14 S2 write commands of the slice: Cancel / Release / SyncDispatch / ReleaseOutboundReservations, the 7 pick
        // commands — claim, bulk assign, confirm, retry, returned, resolve, close short — and the 3 package commands; 0178's count is 99 = 82 + 17;
        // the 2 MOD-0175-FU01 commands stay outside that rule — CT S-101, DÜŞÜK).
        // 106 after MOD-0181 DL-1 (XMC-001, Beste 2026-11-06; pack §5 "sayı + dilimin 0181 yazma komutları"): Create, Update, BulkDelete, Release,
        // Cancel CycleCount — 5 of 0181's 16; measured 101 at 81546c373.
        // 109 after MOD-0181 DL-2 (pack §24 DL-2 seam "CentralAuditDeclarationTests (sayı)"): RecordCountLine, AddCountLine, SubmitCycleCount —
        // 8 of 0181's 16; measured 106 at 081edf122.
        // 116 after MOD-0181 DL-3 (pack §24 DL-3 seam "CentralAuditDeclarationTests"): ApproveCountVariance, RejectCountVariance, RequestCountRecount,
        // RetryCountAdjustment, ResolveCountAdjustmentIntegrity, ReconcilePendingCountAdjustment, UpdateCycleCountSettings — 15 of 0181's 16 (DL-5 adds
        // the 16th); measured 109 at 65af0e638. The sweeper sends its command and opens no transaction: the sweeper list below is unchanged (F-18).
        // 117 after MOD-0181 DL-5 (pack §24 DL-5 seam "CentralAuditDeclarationTests"; XMC-001, Beste 2026-11-12): CreateCountEvidenceExport —
        // 16 of 0181's 16; measured 116 at 200c1a71a. It writes inside CycleCountRepository.InTransactionAsync: the callback count below is unchanged.
        // 123 after MOD-0180 DL-1 (pack §5.1 X-10 "dilim başında ölçülen sayı + dilimin 0180 yazma komutları"; XMC-001, Beste 2026-11-13): CreateWave,
        // UpdateWave, AddWaveOrders, RemoveWaveOrders, CancelWave, UpdateWaveWarehouseSettings — 6 of 0180's 12; measured 117 at 4a64b9119.
        Assert.Equal(123, WriteCommands.Length);
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
            new RecallReadinessManifestProvider(),
            new PutawayPickingPackingManifestProvider(), // MOD-0178 (pack §14.4)
            new Diten.InventoryService.Api.CycleCounting.CycleCountingManifestProvider(), // MOD-0181 (pack §14, XMC-001)
            new Diten.InventoryService.Api.WavePlanning.WavePlanningManifestProvider() // MOD-0180 (pack §14, XMC-001 X-10)
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

        // 9 + RecallDrillRepository.InTransactionAsync (WP-0177-R5) + WarehouseLocationRoleRepository.InTransactionAsync (MOD-0178 DL-1)
        // + WarehouseTaskRepository.InTransactionAsync (MOD-0178 DL-2) + OutboundShipmentRepository.InTransactionAsync (MOD-0178 DL-4)
        // + CycleCountRepository.InTransactionAsync (MOD-0181 DL-1, XMC-001)
        // + WaveRepository.InTransactionAsync (MOD-0180 DL-1, XMC-001 X-10)
        Assert.Equal(15, seen);
        Assert.Empty(failures);
    }
}
