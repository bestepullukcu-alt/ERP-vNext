using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Queries;

/// <summary>getRfxEvent (contract GET /events/{rfxId}). Public RfxId ile; cross-tenant/LE → 404.</summary>
public sealed record GetRfxEventByIdQuery(string RfxId) : IRequest<Response<RfxEventDto>>;
