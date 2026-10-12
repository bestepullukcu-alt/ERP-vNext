using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Returns.Queries;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns.Handlers.QueryHandlers;
public sealed class GetReturnListHandler(IReturnRepository repository,ReturnRequestContext context):IRequestHandler<GetReturnListQuery,Response<ReturnListResponse>>
{
 public async Task<Response<ReturnListResponse>> Handle(GetReturnListQuery request,CancellationToken ct)
 {
  if(request.Status is not null&&!Enum.IsDefined(request.Status.Value))return Response<ReturnListResponse>.Fail("INVALID_REQUEST",400);
  try {var rows=await repository.QueryAsync(context.Scope,request.Status,request.ShipmentId,ct);return Response<ReturnListResponse>.Success(new(rows.Select(r=>new ReturnSummary(r.Id,r.RmaNumber,r.ShipmentId,r.Status.ToString())).ToArray(),rows.Count));}
  catch(ReturnFailureException e){return Response<ReturnListResponse>.Fail(e.Code,e.Status);}
 }
}
