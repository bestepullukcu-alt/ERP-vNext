using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Loads.Commands;
public sealed record CreateLoadCommand(JsonElement Body) : IRequest<Response<LoadResponse>>;
