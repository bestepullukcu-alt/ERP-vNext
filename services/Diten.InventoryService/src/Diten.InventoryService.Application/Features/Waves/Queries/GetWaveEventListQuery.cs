using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Queries;

/// <summary>§4.1 <c>listWaveEvents</c> — the wave's own timeline.</summary>
public sealed record GetWaveEventListQuery(Guid WaveId, string? Page, string? PageSize) : IRequest<Response<WaveEventPageDto>>;
