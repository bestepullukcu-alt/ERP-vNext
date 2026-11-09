using Diten.InventoryService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// MOD-0175-FU01 (Q-03 = A): a command the central-audit record files under the SYSTEM actor — the source-reversal close / mark. It
/// names the REVERSAL it acts on; the record's correlation id is that REVERSAL's (it joins the ledger's <c>InventoryMovementReversed</c> row).
/// </summary>
public interface IQuarantineSystemActorCommand
{
    string ReversalTransactionId { get; }
}

/// <summary>
/// The system-actor mode of Inventory's central-audit scope (pack FU01 §5, XMC-001 Beste 2026-11-03). While a frame is current,
/// <c>InventoryCentralAuditScope</c> answers <c>ActorType System</c>, <see cref="ActorId"/>, no display name and the frame's
/// correlation id; tenant and company stay the resolved scope's. The frame is bound to the dispatch of the marked command itself
/// (<see cref="QuarantineSystemActorBehavior{TRequest,TResponse}"/>, outside <c>CentralAuditBehavior</c>), not to its sender, and is
/// closed when that dispatch ends — error included. An outer command's record was read at its own Begin and does not change.
/// </summary>
public static class QuarantineSystemActor
{
    /// <summary>The fixed system actor of MOD-0175 (pack §14; the intake sweeper's actor).</summary>
    public static readonly Guid ActorId = new("00000000-0000-0000-0000-000000000175");

    private static readonly AsyncLocal<Frame?> Ambient = new();

    public sealed record Frame(Guid CorrelationId);

    /// <summary>The frame of the system-actor command running on this async flow, or null.</summary>
    public static Frame? Current => Ambient.Value;

    internal static Frame? Swap(Frame? frame)
    {
        var outer = Ambient.Value;
        Ambient.Value = frame;
        return outer;
    }
}

/// <summary>
/// Opens the system-actor frame around the dispatch of an <see cref="IQuarantineSystemActorCommand"/> — registered before
/// <c>CentralAuditBehavior</c> (outer), so the record that behavior begins is read inside the frame. Any other request passes through.
/// </summary>
public sealed class QuarantineSystemActorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _services;

    public QuarantineSystemActorBehavior(IServiceProvider services) => _services = services;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IQuarantineSystemActorCommand command)
        {
            return await next();
        }

        // the REVERSAL's own correlation id (a background flow has no request id; the observer's request id would be the same value)
        var correlation = Guid.TryParse(command.ReversalTransactionId, out var id)
                          && await _services.GetRequiredService<IInventoryLedgerRepository>().FindTransactionAsync(id, cancellationToken) is { } reversal
            ? reversal.CorrelationId
            : Guid.Empty;
        var outer = QuarantineSystemActor.Swap(new QuarantineSystemActor.Frame(correlation));
        try
        {
            return await next();
        }
        finally
        {
            QuarantineSystemActor.Swap(outer);
        }
    }
}
