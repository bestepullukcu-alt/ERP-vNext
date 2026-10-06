using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Grn.Commands;

/// <summary>Toplu soft delete (public GrnId listesi; ASSUMPTION-GRN-03). Yalnız Draft olanlar silinir; silinen
/// adedini döner. Hard delete YOK.</summary>
public sealed record BulkDeleteGrnCommand(List<string> GrnIds) : IRequest<Response<int>>;
