using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.QueryHandlers;

/// <summary>
/// DCP-005 Phase 1 — the ONE resolver for controlled-document effectiveness. It reads the tenant's Document Master
/// Register and maps each requested identifier to a disjoint state:
/// <list type="bullet">
/// <item>row found + <see cref="ControlledDocumentLifecyclePolicy.IsOperationallyEffective"/> ⇒ <see cref="DocumentEffectivenessState.Effective"/>;</item>
/// <item>row found + not operationally effective ⇒ <see cref="DocumentEffectivenessState.Blocked"/> (BlockedReason = the register's LifecycleStatus name);</item>
/// <item>no row ⇒ <see cref="DocumentEffectivenessState.Unresolved"/> (a data fact — "no such document").</item>
/// </list>
/// FAIL-CLOSED: the register read is NOT wrapped in a try/catch — an infrastructure failure ("could not check")
/// propagates as a thrown exception and is never converted into an <see cref="DocumentEffectivenessState.Unresolved"/>
/// result (contract §2/§5). Phase 2 resolves via the batched <c>$in</c> repository seam
/// (<see cref="IDocumentMasterRegisterRepository.GetByPermanentUidsAsync"/> / GetByDocumentCodesAsync) — fetching only
/// the requested rows instead of the whole tenant register. The in-memory mapping below is unchanged, so the result is
/// byte-for-byte identical to Phase 1.
/// </summary>
public sealed class ResolveDocumentEffectivenessHandler(
    IDocumentMasterRegisterRepository register,
    ITenantContext tenantContext)
    : IRequestHandler<ResolveDocumentEffectivenessQuery, Response<DocumentEffectivenessResult>>
{
    public async Task<Response<DocumentEffectivenessResult>> Handle(ResolveDocumentEffectivenessQuery request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(tenantContext);

        // Trim + drop blanks, preserving original order and duplicates (one result item per requested occurrence). Full
        // request rejection (empty/all-blank -> 400) is the HTTP boundary's job (Faz 3); here blanks are simply skipped.
        var requested = request.Identifiers
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();

        // Phase 2: batch $in over ONLY the requested identifiers (replaces the Faz 1 full-tenant scan). The read is NOT
        // guarded — a failure here must surface as a thrown exception, not a silent Unresolved.
        var distinct = requested.Distinct(StringComparer.Ordinal).ToList();
        var rows = request.By == DocumentIdentifierKind.Uid
            ? await register.GetByPermanentUidsAsync(distinct, ct)
            : await register.GetByDocumentCodesAsync(distinct, ct);

        // Index by the requested identity field; skip rows whose key is not yet allocated (nullable pre-FU07). Codes /
        // UIDs are unique per tenant (register duplicate guards), so a plain last-wins dictionary is safe.
        var index = new Dictionary<string, DocumentMasterRegisterEntry>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = request.By == DocumentIdentifierKind.Uid ? row.PermanentUid : row.DocumentCode;
            if (!string.IsNullOrWhiteSpace(key))
            {
                index[key.Trim()] = row;
            }
        }

        var items = new List<DocumentEffectivenessItem>(requested.Count);
        foreach (var identifier in requested)
        {
            if (index.TryGetValue(identifier, out var entry))
            {
                var effective = entry.LifecycleStatus.IsOperationallyEffective();
                var lifecycle = entry.LifecycleStatus.ToString();
                items.Add(new DocumentEffectivenessItem(
                    identifier,
                    effective ? DocumentEffectivenessState.Effective : DocumentEffectivenessState.Blocked,
                    entry.DocumentCode,
                    entry.PermanentUid,
                    lifecycle,
                    // BlockedReason is the register's own word: the LifecycleStatus name (null when Effective).
                    effective ? null : lifecycle));
            }
            else
            {
                items.Add(new DocumentEffectivenessItem(identifier, DocumentEffectivenessState.Unresolved, null, null, null, null));
            }
        }

        return Response<DocumentEffectivenessResult>.Success(
            new DocumentEffectivenessResult(items), correlationId: request.CorrelationId);
    }
}
