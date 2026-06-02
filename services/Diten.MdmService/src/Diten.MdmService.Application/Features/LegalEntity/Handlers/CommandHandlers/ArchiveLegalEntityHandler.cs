using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;

public sealed class ArchiveLegalEntityHandler : IRequestHandler<Commands.ArchiveLegalEntityCommand, Response<NoContent>>
{
    private readonly ILegalEntityRepository _repository;

    public ArchiveLegalEntityHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(Commands.ArchiveLegalEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.LegalEntityId, cancellationToken);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Legal Entity not found.", 404);
        }

        if (entity.LifecycleStatus is not LegalEntityLifecycleStatus.Active)
        {
            return Response<NoContent>.Fail("Only active Legal Entities can be archived.", 409);
        }

        entity.LifecycleStatus = LegalEntityLifecycleStatus.Archived;
        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : Response<NoContent>.Fail("Legal Entity not found.", 404);
    }
}
