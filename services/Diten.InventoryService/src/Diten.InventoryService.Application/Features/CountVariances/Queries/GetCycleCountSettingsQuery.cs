using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Queries;

/// <summary><c>getCycleCountSettings</c> (pack §4.4; AC-49) — the record, or the defaults (0 / 0 / 2 / 2000, version 0).</summary>
public sealed record GetCycleCountSettingsQuery : IRequest<Response<CycleCountSettingsDto>>;
