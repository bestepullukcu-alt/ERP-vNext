using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Grn.Commands;

/// <summary>
/// reverseGrn (ASSUMPTION-GRN-02; POST /api/grn/{grnId}/reverse — contract-additive). GRN kaydı EDIT EDİLMEZ;
/// düzeltme INVENTORY REVERSAL/SUPPLIER_RETURN hareketiyle yapılır (append-only, DEC-INV-07). Yalnız Posted →
/// Reversed (aksi 409 INVALID_STATE). Cross-tenant/LE → 404. Idempotency-Key ile idempotent (ikinci REVERSAL yok).
/// </summary>
public sealed record ReverseGrnCommand(string GrnId, string? IdempotencyKey) : IRequest<Response<GrnResponseDto>>;
