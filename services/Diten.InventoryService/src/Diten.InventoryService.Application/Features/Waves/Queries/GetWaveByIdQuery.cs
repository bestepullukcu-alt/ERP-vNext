using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Queries;

/// <summary>§4.1 <c>getWave</c>.</summary>
public sealed record GetWaveByIdQuery(Guid WaveId) : IRequest<Response<WaveDto>>;
