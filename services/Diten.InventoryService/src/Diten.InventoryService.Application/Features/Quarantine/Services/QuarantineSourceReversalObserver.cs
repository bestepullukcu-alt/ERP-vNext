using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// MOD-0175-FU01 (pack §8.2) — the fast path: the ledger's post-commit observer (MOD-0173 seam, no MOD-0173 change) hands every
/// committed REVERSAL to the two source-reversal commands, in the poster's scope. Any other movement is ignored (the intake observer
/// excludes REVERSAL, unchanged). A failure is the ledger's to log; the sweeper closes / marks what this call missed.
/// </summary>
public sealed class QuarantineSourceReversalObserver : IPostedMovementObserver
{
    private readonly ISender _sender;

    public QuarantineSourceReversalObserver(ISender sender) => _sender = sender;

    public Task OnPostedAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default)
        => ApplyAsync(transaction, cancellationToken);

    /// <summary>The two commands' named results (hold, then blocked); none for a movement that is not a REVERSAL.</summary>
    public async Task<IReadOnlyList<Response<SourceReversalResultDto>>> ApplyAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction.MovementType != MovementType.Reversal || transaction.ReversesTransactionId is null)
        {
            return [];
        }

        var reversal = transaction.Id.ToString("D");
        return
        [
            await _sender.Send(new ApplySourceReversalToHoldCommand(reversal), cancellationToken),
            await _sender.Send(new ApplySourceReversalToBlockedCommand(reversal), cancellationToken)
        ];
    }
}
