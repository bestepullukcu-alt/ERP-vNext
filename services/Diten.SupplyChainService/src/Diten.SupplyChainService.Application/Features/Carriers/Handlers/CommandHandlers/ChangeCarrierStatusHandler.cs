using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Carriers.Commands;
using Diten.SupplyChainService.Domain.Features.Carriers;
namespace Diten.SupplyChainService.Application.Features.Carriers.Handlers.CommandHandlers;
public sealed class ChangeCarrierStatusHandler(ICarrierRepository repository, CarrierRequestContext context) : IRequestHandler<ChangeCarrierStatusCommand, Response<CarrierResponse>>
{
    public async Task<Response<CarrierResponse>> Handle(ChangeCarrierStatusCommand request, CancellationToken ct)
    {
        var result = await repository.ChangeStatusAsync(context.Scope, request.CarrierId, context.IdempotencyKey, CarrierRequestFingerprint.Status(request.Body), context.CorrelationId, Enum.Parse<CarrierStatus>(request.Body.TargetStatus!), request.Body.ReasonCode!, ct);
        return result.ErrorCode is null ? Response<CarrierResponse>.Success(new(result.CarrierId, result.CarrierCode, result.Status, result.IdempotentReplay), result.StatusCode)
            : Response<CarrierResponse>.Fail(result.ErrorCode, result.StatusCode);
    }
}
