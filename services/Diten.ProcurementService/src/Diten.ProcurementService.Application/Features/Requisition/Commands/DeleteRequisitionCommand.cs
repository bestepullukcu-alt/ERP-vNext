using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Requisition.Commands;

/// <summary>Soft delete tek requisition (public RequisitionId). Yalnız Draft silinebilir (Draft dışı → 409). Hard delete YOK.</summary>
public sealed record DeleteRequisitionCommand(string RequisitionId) : IRequest<Response<bool>>;
