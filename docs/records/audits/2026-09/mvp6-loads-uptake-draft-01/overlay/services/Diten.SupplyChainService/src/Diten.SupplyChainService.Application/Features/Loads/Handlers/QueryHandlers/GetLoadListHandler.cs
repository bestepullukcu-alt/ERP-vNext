using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Loads.Queries;
using Diten.SupplyChainService.Domain.Features.Loads;
namespace Diten.SupplyChainService.Application.Features.Loads.Handlers.QueryHandlers;
public sealed class GetLoadListHandler(ILoadRepository repository,LoadRequestContext context):IRequestHandler<GetLoadListQuery,Response<LoadListResponse>>
{ public async Task<Response<LoadListResponse>> Handle(GetLoadListQuery request,CancellationToken ct)
 { try { var rows=await repository.QueryAsync(context.Scope,request.Status is null?null:Enum.Parse<LoadStatus>(request.Status),request.CarrierId is null?null:Guid.Parse(request.CarrierId),ct);return Response<LoadListResponse>.Success(new(rows.Select(x=>new LoadSummary(x.Load.Id,x.Load.LoadNumber,x.Load.CarrierId,x.Load.ShipmentIds,x.Load.Status.ToString(),x.RootState==LoadRootState.PresentStoredUuid?x.LifecycleCorrelationId:null)).ToArray(),rows.Count)); } catch(LoadFailureException ex) {return Response<LoadListResponse>.Fail(ex.Code,ex.Status);} } }
