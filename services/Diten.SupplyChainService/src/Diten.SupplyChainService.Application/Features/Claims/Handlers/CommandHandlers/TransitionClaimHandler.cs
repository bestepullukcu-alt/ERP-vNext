using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Claims;
using Diten.SupplyChainService.Application.Features.Claims.Commands;
namespace Diten.SupplyChainService.Application.Features.Claims.Handlers.CommandHandlers;
public sealed class TransitionClaimHandler(IClaimRepository repository,ClaimRequestContext context,IClaimReferenceReader reader):IRequestHandler<TransitionClaimCommand,Response<ClaimResponse>>
{
 public async Task<Response<ClaimResponse>> Handle(TransitionClaimCommand request,CancellationToken ct)
 {
  if(!new[]{"create","investigate","decide","settle"}.Any(p=>context.Permissions.Contains("supplychain.claims."+p)))return Response<ClaimResponse>.Fail("FORBIDDEN",403);
  if(!ClaimWire.TransitionValid(request.Body))return Response<ClaimResponse>.Fail("INVALID_REQUEST",400);
  var target=Enum.Parse<ClaimStatus>(request.Body.GetProperty("targetStatus").GetString()!);
  if(!context.Permissions.Contains(ClaimWire.Permission(target)))return Response<ClaimResponse>.Fail("FORBIDDEN",403);
  try {
   var r=await repository.MutateAsync(context.Scope,request.ClaimId,context.IdempotencyKey,ClaimRequestFingerprint.Transition(request.Body),context.CorrelationId,null,target,request.Body.GetProperty("occurredAt").GetString(),ClaimWire.Optional(request.Body,"approvedAmount"),ClaimWire.Optional(request.Body,"resolutionCode"),ClaimWire.Optional(request.Body,"note"),reader.ObserveAsync,ct);
   return r.ErrorCode is null?Response<ClaimResponse>.Success(new(r.ClaimId,r.ClaimNumber,r.ShipmentId,r.Status,r.ApprovedAmount,r.IdempotentReplay),r.StatusCode):Response<ClaimResponse>.Fail(r.ErrorCode,r.StatusCode);
  }catch(ClaimFailureException ex){return Response<ClaimResponse>.Fail(ex.Code,ex.Status);}
 }
}
