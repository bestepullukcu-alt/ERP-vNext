using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Queries;

/// <summary><c>getCountVariance</c> — one counted line with its count number and its ledger correction (pack §8.13).</summary>
public sealed record GetCountVarianceByIdQuery(Guid LineId) : IRequest<Response<CountVarianceDto>>;
