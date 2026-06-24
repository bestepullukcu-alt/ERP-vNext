using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;

public sealed class UpdateLegalEntityHandler : IRequestHandler<Commands.UpdateLegalEntityCommand, Response<NoContent>>
{
    private readonly ILegalEntityRepository _repository;

    public UpdateLegalEntityHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(Commands.UpdateLegalEntityCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.LegalEntityId, cancellationToken);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Legal Entity not found.", 404);
        }

        var r = request.Request;
        var normalizedCode = r.Code.Trim();
        if (await _repository.ExistsByCodeAsync(normalizedCode, excludeId: entity.Id, cancellationToken: cancellationToken))
        {
            return Response<NoContent>.Fail("A Legal Entity with this code already exists.", 409);
        }

        var parentId = r.ParentLegalEntityId;
        if (parentId is { } pid && pid != Guid.Empty)
        {
            if (pid == entity.Id)
            {
                return Response<NoContent>.Fail("A Legal Entity cannot be its own parent.", 400);
            }

            var parent = await _repository.GetByIdAsync(pid, cancellationToken);
            if (parent is null)
            {
                return Response<NoContent>.Fail("Parent Legal Entity not found.", 400);
            }
        }

        // OperationalStatus is preserved (lifecycle-owned); Apply maps every editable field + recomputes completeness.
        LegalEntityMappings.Apply(entity, r);

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : Response<NoContent>.Fail("Legal Entity not found.", 404);
    }
}
