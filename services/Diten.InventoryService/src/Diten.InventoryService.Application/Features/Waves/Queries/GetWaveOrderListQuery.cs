using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Queries;

/// <summary>§4.1 <c>listWaveOrders</c>.</summary>
public sealed record GetWaveOrderListQuery(Guid WaveId, IReadOnlyList<string> State, string? Page, string? PageSize) : IRequest<Response<WaveOrderPageDto>>;
