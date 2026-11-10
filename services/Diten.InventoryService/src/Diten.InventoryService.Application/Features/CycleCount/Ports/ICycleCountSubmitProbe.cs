namespace Diten.InventoryService.Application.Features.CycleCounting.Ports;

/// <summary>
/// Test seam of the submit's ledger read (pack §8.4, AC-20): called in Persistence INSIDE the read's error boundary, before the ledger is read.
/// Production: <c>NoopCycleCountSubmitProbe</c> (does nothing). A test makes it throw a driver error to stand for an unreadable ledger — the
/// production code then has to answer 503 <c>COUNT_INVENTORY_UNAVAILABLE</c> and write nothing.
/// </summary>
public interface ICycleCountSubmitProbe
{
    Task BeforeLedgerReadAsync(CancellationToken cancellationToken = default);
}
