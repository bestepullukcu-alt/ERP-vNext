namespace Diten.InventoryService.Application.Features.CycleCounting.Ports;

/// <summary>
/// Pack §8.2 rule 4 "Test kancası" (r5, SB1/F-3): the release's snapshot read calls this BETWEEN its balance read and its movement-id read.
/// Production registers <c>NoopCycleCountReleaseProbe</c> (does nothing); a test posts a ledger movement here to prove that both reads
/// come from ONE snapshot (AC-52 (a)) — with separate reads the movement would be missing from the balances but present in the id list.
/// </summary>
public interface ICycleCountReleaseProbe
{
    Task AfterBalanceReadAsync(CancellationToken cancellationToken = default);
}
