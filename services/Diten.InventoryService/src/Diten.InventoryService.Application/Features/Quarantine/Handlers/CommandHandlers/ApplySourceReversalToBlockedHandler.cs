using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Enums;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers.CommandHandlers;

/// <summary>MOD-0175-FU01 — the source movement of a blocked-stock record was reversed: pack §8.3 (<see cref="QuarantineSourceReversal"/>).</summary>
public sealed class ApplySourceReversalToBlockedHandler : IRequestHandler<ApplySourceReversalToBlockedCommand, Response<SourceReversalResultDto>>
{
    private readonly QuarantineSourceReversal _reversal;

    public ApplySourceReversalToBlockedHandler(QuarantineSourceReversal reversal) => _reversal = reversal;

    public Task<Response<SourceReversalResultDto>> Handle(ApplySourceReversalToBlockedCommand request, CancellationToken cancellationToken)
        => _reversal.ApplyAsync(HistorySubject.Blocked, request.ReversalTransactionId, cancellationToken);
}
