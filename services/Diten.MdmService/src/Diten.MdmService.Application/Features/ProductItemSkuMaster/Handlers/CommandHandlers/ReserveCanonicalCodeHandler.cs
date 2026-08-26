using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;

public sealed class ReserveCanonicalCodeHandler
    : IRequestHandler<ReserveCanonicalCodeCommand, Response<ProductItemSkuMasterModels.CodeReservationDto>>
{
    private readonly ICodeReservationRepository _repository;
    private readonly IProductIdentityActorContext _actorContext;

    public ReserveCanonicalCodeHandler(
        ICodeReservationRepository repository,
        IProductIdentityActorContext actorContext)
    {
        _repository = repository;
        _actorContext = actorContext;
    }

    public async Task<Response<ProductItemSkuMasterModels.CodeReservationDto>> Handle(
        ReserveCanonicalCodeCommand request,
        CancellationToken cancellationToken)
    {
        var reservation = await _repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct,
            request.Request.IdempotencyKey,
            _actorContext.ActorId,
            request.Request.IdempotencyKey,
            cancellationToken);

        var dto = new ProductItemSkuMasterModels.CodeReservationDto(
            reservation.Id,
            reservation.ReservedCode,
            reservation.EntityType,
            reservation.ReservationState,
            reservation.BindingState,
            reservation.Version);

        return Response<ProductItemSkuMasterModels.CodeReservationDto>.Success(dto, 201);
    }
}
