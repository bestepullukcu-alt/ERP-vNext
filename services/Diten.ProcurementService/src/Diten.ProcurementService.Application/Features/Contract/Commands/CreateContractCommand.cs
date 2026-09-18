using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Contract.Commands;

/// <summary>
/// createContract (contract POST /api/contracts). Tenant/LE server-resolved — payload'da YOK. Idempotency-Key ile
/// idempotent (replay → aynı sözleşme, yeni kayıt YOK). supplierId MOD-0140 CONSUME (fail-closed → 404
/// UNKNOWN_REFERENCE); rfxId verilirse MOD-0145 award CONSUME (fail-closed → 404). effectiveFrom zorunlu; effectiveTo
/// verilirse ≥ effectiveFrom (aksi 422). currency boşsa server LE base default (ASSUMPTION-0144-04). clauses[].clauseId
/// clause library'de var olmalı (yoksa 422). evidenceRefs yalnız REFERANS (MOD-0029/0031); binary saklanmaz.
/// Sözleşme Draft doğar.
/// </summary>
public sealed record CreateContractCommand(
    string SupplierId,
    string? RfxId,
    string Title,
    string EffectiveFrom,
    string? EffectiveTo,
    string? Currency,
    List<ClauseRefInput>? Clauses,
    List<string>? EvidenceRefs,
    string? IdempotencyKey) : IRequest<Response<ContractDto>>;
