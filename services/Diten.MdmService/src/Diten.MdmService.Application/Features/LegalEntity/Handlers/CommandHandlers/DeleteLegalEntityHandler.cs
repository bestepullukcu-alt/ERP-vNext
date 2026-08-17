using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;

public sealed class DeleteLegalEntityHandler : IRequestHandler<Commands.DeleteLegalEntityCommand, Response<NoContent>>
{
    private readonly ILegalEntityRepository _repository;

    public DeleteLegalEntityHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(Commands.DeleteLegalEntityCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(request.LegalEntityId, cancellationToken);
        return deleted
            ? Response<NoContent>.SuccessWithoutData(204)
            : Response<NoContent>.Fail("Legal Entity not found.", 404);
    }
}
