using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Queries;

/// <summary>§4.1 <c>listWaves</c> — the raw query parameters (parsed by the handler; a bad one is 400).</summary>
public sealed record GetWaveListQuery(string? WarehouseId, IReadOnlyList<string> Status, string? WaveType, string? Q, string? CutoffFrom, string? CutoffTo,
    string? Page, string? PageSize) : IRequest<Response<WavePageDto>>;
