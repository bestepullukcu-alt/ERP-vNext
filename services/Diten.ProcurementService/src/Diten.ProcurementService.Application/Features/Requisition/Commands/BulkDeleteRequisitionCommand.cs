using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Requisition.Commands;

/// <summary>Toplu soft delete (public RequisitionId listesi). Yalnız Draft olanlar silinir; silinen adedini döner.</summary>
public sealed record BulkDeleteRequisitionCommand(List<string> RequisitionIds) : IRequest<Response<int>>;
