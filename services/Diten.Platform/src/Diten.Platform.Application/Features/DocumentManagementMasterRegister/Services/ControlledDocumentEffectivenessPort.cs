using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;

/// <summary>
/// DCP-005 Phase 1 — the in-process gate implementation: a thin wrapper over <see cref="IMediator"/> that routes to the
/// single <see cref="ResolveDocumentEffectivenessQuery"/> resolver. It owns the correlation id the CorrelationId-free
/// port contract (§3) omits, and unwraps the resolver's <c>Response&lt;T&gt;</c> envelope.
///
/// FAIL-CLOSED: the resolver's read is not caught anywhere below it, so an infrastructure failure throws straight
/// through this port (the "could not check" branch). Should the resolver ever return an unsuccessful envelope, this
/// port throws rather than fabricating a result — an effectiveness answer is never invented.
/// </summary>
public sealed class ControlledDocumentEffectivenessPort(IMediator mediator) : IControlledDocumentEffectivenessPort
{
    public async Task<DocumentEffectivenessResult> ResolveAsync(DocumentEffectivenessQuery query, CancellationToken ct)
    {
        var response = await mediator.Send(
            new ResolveDocumentEffectivenessQuery(query.Identifiers, query.By, Guid.NewGuid().ToString("N")), ct);

        if (!response.IsSuccessful || response.Data is null)
        {
            // Not the Unresolved data-fact branch: an unsuccessful envelope is an infrastructure/contract failure, so
            // it surfaces as a thrown exception for the caller to refuse activation on (contract §2/§5).
            throw new InvalidOperationException(
                "Controlled-document effectiveness could not be resolved.");
        }

        return response.Data;
    }
}
