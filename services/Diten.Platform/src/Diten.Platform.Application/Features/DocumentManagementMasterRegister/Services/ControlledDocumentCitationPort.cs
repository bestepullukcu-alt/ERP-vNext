using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;

/// <summary>
/// DCP-005 Phase 2a — the in-process citation gate implementation: a thin wrapper over <see cref="IMediator"/> that
/// routes to the single citation resolver. It owns the correlation id the CorrelationId-free port contract omits, and
/// unwraps the resolver's <c>Response&lt;T&gt;</c> envelope. Mirrors <see cref="ControlledDocumentEffectivenessPort"/>.
///
/// FAIL-CLOSED: the resolver's read is not caught below it, so an infrastructure failure throws straight through. An
/// unsuccessful envelope is thrown rather than fabricated into an empty result — a citation answer is never invented.
/// </summary>
public sealed class ControlledDocumentCitationPort(IMediator mediator) : IControlledDocumentCitationPort
{
    public async Task<DocumentCitationResult> ResolveAsync(DocumentCitationQuery query, CancellationToken ct)
    {
        var response = await mediator.Send(
            new ResolveDocumentCitationQuery(query.Identifiers, query.By, Guid.NewGuid().ToString("N")), ct);

        return Unwrap(response);
    }

    public async Task<DocumentCitationResult> SearchAsync(string? term, int limit, CancellationToken ct)
    {
        var response = await mediator.Send(
            new SearchDocumentCitationQuery(term, limit, Guid.NewGuid().ToString("N")), ct);

        return Unwrap(response);
    }

    private static DocumentCitationResult Unwrap(Common.Response<DocumentCitationResult> response)
    {
        if (!response.IsSuccessful || response.Data is null)
        {
            // An unsuccessful envelope is an infrastructure/contract failure, not an empty citation list, so it
            // surfaces as a thrown exception for the caller (contract §2/§5) — never a fabricated result.
            throw new InvalidOperationException("Controlled-document citations could not be resolved.");
        }

        return response.Data;
    }
}
