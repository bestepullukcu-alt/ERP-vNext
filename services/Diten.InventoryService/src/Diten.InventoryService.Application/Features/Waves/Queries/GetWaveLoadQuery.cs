using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Queries;

/// <summary>§4.1 <c>getWaveLoad</c> — the load tab (zones / aisles, pickers).</summary>
public sealed record GetWaveLoadQuery(Guid WaveId) : IRequest<Response<WaveLoadDto>>;
