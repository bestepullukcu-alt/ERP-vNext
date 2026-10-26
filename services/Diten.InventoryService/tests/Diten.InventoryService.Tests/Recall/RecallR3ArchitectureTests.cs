using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Tests.Architecture;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R3 guards on the production sources (comments stripped): the recall exit block never reads <c>sourceModule</c> (CT-R11, O-F2-2);
/// the recall code never touches a reservation (CT-R2, 0175 CT-Q6); the recall port sits after the lot rules and BEFORE the expiry hooks at
/// both seams (pre-check in the validator, re-check in the posting transaction — CT-R11).
/// </summary>
public sealed class RecallR3ArchitectureTests
{
    private static SourceScan.SourceFile File(string project, string path)
        => Assert.Single(SourceScan.Production(project), f => f.RelativePath.EndsWith(path, StringComparison.Ordinal));

    [Fact]
    public void The_recall_exit_block_never_reads_sourceModule()
    {
        Assert.DoesNotContain("SourceModule", File("Diten.InventoryService.Persistence", "Recall/RecallPostingGate.cs").Body, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceModule", File("Diten.InventoryService.Domain", "Recall/Rules/RecallExitRule.cs").Body, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(RecallPostingRequest).GetProperties(), p => p.Name.Contains("Source", StringComparison.Ordinal));
    }

    [Fact]
    public void No_recall_source_writes_or_cancels_a_reservation()
    {
        foreach (var file in SourceScan.Production("Diten.InventoryService.Application").Concat(SourceScan.Production("Diten.InventoryService.Persistence"))
                     .Where(f => f.RelativePath.Contains("Recall", StringComparison.Ordinal)))
        {
            Assert.DoesNotMatch(@"IInventoryReservationRepository|ReservationPositionOperations|CancelReservation|ReleaseReservation|inventory_reservations|ReserveCommand",
                file.Body);
        }
    }

    [Fact]
    public void The_recall_port_runs_after_the_lot_rules_and_before_the_expiry_hooks_at_both_seams()
    {
        var validator = File("Diten.InventoryService.Application", "Features/Inventory/Services/MovementMasterDataValidator.cs").Body;
        var lotRules = validator.IndexOf("LotPostingRules.Evaluate(lot, movement.SkuId", StringComparison.Ordinal);
        var recallPre = validator.IndexOf("lotId, lot, movement.SkuId, movement.SkuLevel, movement.IdempotencyKey)", StringComparison.Ordinal);
        var expiryPre = validator.IndexOf("_expiry.PreCheckAsync(", StringComparison.Ordinal);
        Assert.True(lotRules > 0 && recallPre > lotRules && expiryPre > recallPre, $"validator order: lot rules {lotRules}, recall {recallPre}, expiry {expiryPre}");

        var ledger = File("Diten.InventoryService.Persistence", "Repositories/InventoryLedgerRepository.cs").Body;
        var resolve = ledger.IndexOf("var lot = await ResolvePostingLotAsync(transaction, ct);", StringComparison.Ordinal);
        var recallRe = ledger.IndexOf("_recall.ReCheckAsync(transaction, lot, ct)", StringComparison.Ordinal);
        var expiryRe = ledger.IndexOf("_expiry.ReCheckAsync(transaction, lot,", StringComparison.Ordinal);
        Assert.True(resolve > 0 && recallRe > resolve && expiryRe > recallRe, $"posting order: lot {resolve}, recall {recallRe}, expiry {expiryRe}");
    }
}
