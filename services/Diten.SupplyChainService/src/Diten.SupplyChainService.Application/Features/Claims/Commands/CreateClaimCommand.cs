using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Claims.Commands;
public sealed record CreateClaimCommand(JsonElement Body):IRequest<Response<ClaimResponse>>;
