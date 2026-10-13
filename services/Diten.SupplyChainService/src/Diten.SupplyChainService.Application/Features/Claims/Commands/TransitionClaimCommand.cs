using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Claims.Commands;
public sealed record TransitionClaimCommand(Guid ClaimId,JsonElement Body):IRequest<Response<ClaimResponse>>;
