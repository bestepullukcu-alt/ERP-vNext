using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Returns.Queries;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns.Handlers.QueryHandlers;
/// <summary>MOD-0186 r10 §35.6 (RB-8): own tenant and company → 200; another tenant, another company or an unknown id → one
/// 404 RETURN_NOT_FOUND. The repository read is filtered on tenant, legal entity and IsDeleted = false.</summary>
public sealed class GetReturnByIdHandler(IReturnRepository repository,ReturnRequestContext context):IRequestHandler<GetReturnByIdQuery,Response<ReturnDetail>>
{
 public async Task<Response<ReturnDetail>> Handle(GetReturnByIdQuery request,CancellationToken ct)
 {
  if(request.ReturnId==Guid.Empty)return Response<ReturnDetail>.Fail("INVALID_REQUEST",400);
  try {var order=await repository.GetAsync(context.Scope,request.ReturnId,ct);
   return order is null?Response<ReturnDetail>.Fail("RETURN_NOT_FOUND",404):Response<ReturnDetail>.Success(ReturnDetail.From(order));}
  catch(ReturnFailureException e){return Response<ReturnDetail>.Fail(e.Code,e.Status);}
 }
}
