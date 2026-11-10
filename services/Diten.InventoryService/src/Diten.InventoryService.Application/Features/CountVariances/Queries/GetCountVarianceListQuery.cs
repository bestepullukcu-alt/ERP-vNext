using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Queries;

/// <summary>
/// <c>listCountVariances</c> (pack §2.4, §8.13): <c>source</c> = <c>count</c> (default) | <c>manual</c> | <c>all</c>; <c>manual</c> / <c>all</c> need a warehouse
/// and a window of at most 31 days. Raw texts — the handler parses them (400 on a bad value).
/// </summary>
public sealed record GetCountVarianceListQuery(string? Source, string? WarehouseId, string? From, string? To, IReadOnlyList<string> Statuses, string? SkuId,
    string? LotId, string? Page, string? PageSize) : IRequest<Response<CountVariancePageDto>>;
