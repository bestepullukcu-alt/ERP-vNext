using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Claims;
using Diten.SupplyChainService.Application.Features.Claims.Queries;
namespace Diten.SupplyChainService.Application.Features.Claims.Handlers.QueryHandlers;
public sealed class GetClaimListHandler(IClaimRepository repository,ClaimRequestContext context):IRequestHandler<GetClaimListQuery,Response<ClaimListResponse>>
{
 public async Task<Response<ClaimListResponse>> Handle(GetClaimListQuery request,CancellationToken ct)
 {
  if(!context.Permissions.Contains("supplychain.claims.read"))return Response<ClaimListResponse>.Fail("FORBIDDEN",403);
  if(request.Status is not null&&!Enum.GetNames<ClaimStatus>().Contains(request.Status)||request.ShipmentId is not null&&!ClaimWire.Uuid(request.ShipmentId))return Response<ClaimListResponse>.Fail("INVALID_REQUEST",400);
  try{var rows=await repository.QueryAsync(context.Scope,request.Status is null?null:Enum.Parse<ClaimStatus>(request.Status),request.ShipmentId is null?null:Guid.Parse(request.ShipmentId),ct);
   return Response<ClaimListResponse>.Success(new(rows.Select(x=>new ClaimSummary(x.Id,x.ClaimNumber,x.ShipmentId,x.Status.ToString(),x.ClaimedAmount,x.Currency)).ToArray(),rows.Count));
  }catch(ClaimFailureException ex){return Response<ClaimListResponse>.Fail(ex.Code,ex.Status);}
 }
}
