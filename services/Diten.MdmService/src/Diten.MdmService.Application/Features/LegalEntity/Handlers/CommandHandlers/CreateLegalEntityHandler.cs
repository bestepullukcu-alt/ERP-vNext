using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;

public sealed class CreateLegalEntityHandler : IRequestHandler<Commands.CreateLegalEntityCommand, Response<Guid>>
{
    private readonly ILegalEntityRepository _repository;

    public CreateLegalEntityHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(Commands.CreateLegalEntityCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var normalizedCode = r.Code.Trim();
        if (await _repository.ExistsByCodeAsync(normalizedCode, cancellationToken: cancellationToken))
        {
            return Response<Guid>.Fail("A Legal Entity with this code already exists.", 409);
        }

        // Referential integrity: a declared parent must exist within the same tenant.
        if (r.ParentLegalEntityId is { } parentId && parentId != Guid.Empty)
        {
            var parent = await _repository.GetByIdAsync(parentId, cancellationToken);
            if (parent is null)
            {
                return Response<Guid>.Fail("Parent Legal Entity not found.", 400);
            }
        }

        // New entities always start in Draft (lifecycle owns OperationalStatus); activation is a separate step.
        var entity = new Domain.Entities.LegalEntity();
        LegalEntityMappings.Apply(entity, r);

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<Guid>.Success(created.Id, 201);
    }
}
