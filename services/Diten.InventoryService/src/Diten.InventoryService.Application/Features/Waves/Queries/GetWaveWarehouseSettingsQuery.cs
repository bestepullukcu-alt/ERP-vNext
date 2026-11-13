using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Queries;

/// <summary>§4.1 <c>getWaveWarehouseSettings</c> — the stored setting, or the defaults with version 0.</summary>
public sealed record GetWaveWarehouseSettingsQuery(Guid WarehouseId) : IRequest<Response<WaveWarehouseSettingsDto>>;
