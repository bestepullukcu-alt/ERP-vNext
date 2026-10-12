using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Carriers.Commands;
using Diten.SupplyChainService.Domain.Features.Carriers;
namespace Diten.SupplyChainService.Application.Features.Carriers.Handlers.CommandHandlers;
public sealed class CreateCarrierHandler(ICarrierRepository repository, CarrierRequestContext context) : IRequestHandler<CreateCarrierCommand, Response<CarrierResponse>>
{
    public async Task<Response<CarrierResponse>> Handle(CreateCarrierCommand request, CancellationToken ct)
    {
        var result = await repository.CreateAsync(context.Scope, context.IdempotencyKey, CarrierRequestFingerprint.Create(request.Body), context.CorrelationId, request.Body.CarrierCode!, request.Body.DisplayName!, request.Body.SupportedModes!, request.Body.ExternalReference, ct);
        return result.ErrorCode is null ? Response<CarrierResponse>.Success(new(result.CarrierId, result.CarrierCode, result.Status, result.IdempotentReplay), result.StatusCode)
            : Response<CarrierResponse>.Fail(result.ErrorCode, result.StatusCode);
    }
}
