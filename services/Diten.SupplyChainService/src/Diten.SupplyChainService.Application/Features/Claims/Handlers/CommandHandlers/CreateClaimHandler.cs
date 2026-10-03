using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Claims;
using Diten.SupplyChainService.Application.Features.Claims.Commands;
namespace Diten.SupplyChainService.Application.Features.Claims.Handlers.CommandHandlers;
public sealed class CreateClaimHandler(IClaimRepository repository,ClaimRequestContext context,IClaimReferenceReader reader):IRequestHandler<CreateClaimCommand,Response<ClaimResponse>>
{
 public async Task<Response<ClaimResponse>> Handle(CreateClaimCommand request,CancellationToken ct)
 {
  if(!context.Permissions.Contains("supplychain.claims.create"))return Response<ClaimResponse>.Fail("FORBIDDEN",403);
  if(!ClaimWire.CreateValid(request.Body))return Response<ClaimResponse>.Fail("INVALID_REQUEST",400);
  try {
   var r=await repository.MutateAsync(context.Scope,null,context.IdempotencyKey,ClaimRequestFingerprint.Create(request.Body),context.CorrelationId,ClaimWire.Plan(request.Body),null,null,null,null,null,reader.ObserveAsync,ct);
   return r.ErrorCode is null?Response<ClaimResponse>.Success(new(r.ClaimId,r.ClaimNumber,r.ShipmentId,r.Status,r.ApprovedAmount,r.IdempotentReplay),r.StatusCode):Response<ClaimResponse>.Fail(r.ErrorCode,r.StatusCode);
  }catch(ClaimFailureException ex){return Response<ClaimResponse>.Fail(ex.Code,ex.Status);}
 }
}
