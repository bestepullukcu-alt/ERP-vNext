using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Returns.Commands;
public sealed record CreateReturnCommand(JsonElement Body):IRequest<Response<ReturnResponse>>;
