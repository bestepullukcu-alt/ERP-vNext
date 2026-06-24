using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;

public sealed class SuspendLegalEntityHandler : IRequestHandler<Commands.SuspendLegalEntityCommand, Response<NoContent>>
{
    private readonly ILegalEntityRepository _repository;

    public SuspendLegalEntityHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(Commands.SuspendLegalEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.LegalEntityId, cancellationToken);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Legal Entity not found.", 404);
        }

        if (entity.OperationalStatus is not LegalEntityOperationalStatus.Active)
        {
            return Response<NoContent>.Fail("Only active Legal Entities can be suspended.", 409);
        }

        entity.OperationalStatus = LegalEntityOperationalStatus.Suspended;
        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : Response<NoContent>.Fail("Legal Entity not found.", 404);
    }
}
