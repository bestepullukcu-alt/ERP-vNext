using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Contract.Commands;

/// <summary>
/// updateContract (ASSUMPTION-0144-01 additive; pack §14 procurement.contracts.update). Yalnız Draft güncellenebilir
/// (InReview/Active/Expired/Terminated → 409 INVALID_STATE; aktive edilmiş sözleşme + approval trail dondurulur).
/// SupplierId IMMUTABLE (kimlik) — güncellenmez. rfxId verilirse MOD-0145 award CONSUME (fail-closed → 404). clauseId'ler
/// clause library'de var olmalı (yoksa 422). Optimistic concurrency; cross-tenant/LE → 404. evidenceRef yalnız
/// referans (binary YOK).
/// </summary>
public sealed record UpdateContractCommand(
    string ContractId,
    string? RfxId,
    string Title,
    string EffectiveFrom,
    string? EffectiveTo,
    string? Currency,
    List<ClauseRefInput>? Clauses,
    List<string>? EvidenceRefs) : IRequest<Response<ContractDto>>;
