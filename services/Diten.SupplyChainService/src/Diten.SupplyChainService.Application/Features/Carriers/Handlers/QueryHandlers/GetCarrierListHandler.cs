using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Carriers.Queries;
using Diten.SupplyChainService.Domain.Features.Carriers;
namespace Diten.SupplyChainService.Application.Features.Carriers.Handlers.QueryHandlers;
public sealed class GetCarrierListHandler(ICarrierRepository repository, CarrierRequestContext context) : IRequestHandler<GetCarrierListQuery, Response<CarrierListResponse>>
{
    public async Task<Response<CarrierListResponse>> Handle(GetCarrierListQuery request, CancellationToken ct)
    {
        try
        {
        var values = await repository.QueryAsync(context.Scope, request.Status is null ? null : Enum.Parse<CarrierStatus>(request.Status), ct);
        var items = values.Select(x => new CarrierSummary(x.Id, x.CarrierCode, x.DisplayName, x.Status.ToString(), x.SupportedModes)).ToArray();
        return Response<CarrierListResponse>.Success(new(items, items.Length));
        }
        catch (CarrierPersistenceUnavailableException) { return Response<CarrierListResponse>.Fail("PERSISTENCE_UNAVAILABLE", 503); }
    }
}
