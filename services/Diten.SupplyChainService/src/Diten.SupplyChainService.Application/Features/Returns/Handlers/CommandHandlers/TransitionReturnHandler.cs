using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Returns.Commands;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns.Handlers.CommandHandlers;
public sealed class TransitionReturnHandler(IReturnRepository repository,ReturnRequestContext context):IRequestHandler<TransitionReturnCommand,Response<ReturnResponse>>
{
 public async Task<Response<ReturnResponse>> Handle(TransitionReturnCommand request,CancellationToken ct)
 {
  if(!ReturnWire.TransitionValid(request.Body))return Response<ReturnResponse>.Fail("INVALID_REQUEST",400);
  try {
   var b=request.Body;var r=await repository.MutateAsync(context.Scope,request.ReturnId,context.IdempotencyKey,ReturnRequestFingerprint.Transition(b),context.CorrelationId,null,Enum.Parse<ReturnStatus>(b.GetProperty("targetStatus").GetString()!),b.GetProperty("occurredAt").GetString(),ReturnWire.OptionalText(b,"inventoryTransactionReferenceId"),ReturnWire.OptionalText(b,"dispositionCode"),(_,_)=>throw new InvalidOperationException("Transitions must not read external references."),ct);
   return r.ErrorCode is null?Response<ReturnResponse>.Success(new(r.ReturnId,r.RmaNumber,r.ShipmentId,r.Status,r.IdempotentReplay),r.StatusCode):Response<ReturnResponse>.Fail(r.ErrorCode,r.StatusCode);
  }catch(ReturnFailureException e){return Response<ReturnResponse>.Fail(e.Code,e.Status);}
 }
}
