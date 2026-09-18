using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Commands;

/// <summary>Toplu soft delete (public RfxId listesi). Yalnız Draft olanlar silinir; silinen adedini döner.</summary>
public sealed record BulkDeleteRfxEventCommand(List<string> RfxIds) : IRequest<Response<int>>;
