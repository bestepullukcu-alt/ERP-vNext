using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Grn.Queries;

/// <summary>getGrn (contract GET /api/grn/{grnId}). Cross-tenant/LE → 404 UNKNOWN_GRN (sızıntı yok).</summary>
public sealed record GetGrnByIdQuery(string GrnId) : IRequest<Response<GrnResponseDto>>;
