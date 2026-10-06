using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Commands;

/// <summary>Soft delete tek RFx (public RfxId). Yalnız Draft silinebilir (Draft dışı → 409). Hard delete YOK.</summary>
public sealed record DeleteRfxEventCommand(string RfxId) : IRequest<Response<bool>>;
