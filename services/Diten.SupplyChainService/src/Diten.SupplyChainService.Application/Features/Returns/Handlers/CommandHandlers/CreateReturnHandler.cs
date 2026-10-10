using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Returns.Commands;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns.Handlers.CommandHandlers;
public sealed class CreateReturnHandler(IReturnRepository repository,ReturnRequestContext context,IReturnReferenceReader reader):IRequestHandler<CreateReturnCommand,Response<ReturnResponse>>
{
 public async Task<Response<ReturnResponse>> Handle(CreateReturnCommand request,CancellationToken ct)
 {
  if(!ReturnWire.CreateValid(request.Body))return Response<ReturnResponse>.Fail("INVALID_REQUEST",400);
  try {
   var r=await repository.MutateAsync(context.Scope,null,context.IdempotencyKey,ReturnRequestFingerprint.Create(request.Body),context.CorrelationId,ReturnWire.Plan(request.Body),null,null,null,null,reader.ObserveAsync,ct,request.Body.GetRawText());
   return r.ErrorCode is null?Response<ReturnResponse>.Success(new(r.ReturnId,r.RmaNumber,r.ShipmentId,r.Status,r.IdempotentReplay),r.StatusCode):Response<ReturnResponse>.Fail(r.ErrorCode,r.StatusCode);
  }catch(ReturnFailureException e){return Response<ReturnResponse>.Fail(e.Code,e.Status);}
 }
}
