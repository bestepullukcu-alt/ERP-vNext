using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;

public sealed class SetLegalEntityParentHandler : IRequestHandler<Commands.SetLegalEntityParentCommand, Response<NoContent>>
{
    private readonly ILegalEntityRepository _repository;
    private readonly ILegalEntityHierarchyResolver _hierarchyResolver;

    public SetLegalEntityParentHandler(ILegalEntityRepository repository, ILegalEntityHierarchyResolver hierarchyResolver)
    {
        _repository = repository;
        _hierarchyResolver = hierarchyResolver;
    }

    public async Task<Response<NoContent>> Handle(Commands.SetLegalEntityParentCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.LegalEntityId, cancellationToken);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Legal Entity not found.", 404);
        }

        if (request.ParentId.HasValue)
        {
            var parentId = request.ParentId.Value;

            if (parentId == entity.Id)
            {
                return Response<NoContent>.Fail("A Legal Entity cannot be its own parent.", 400);
            }

            // Tenant-scoped lookup: a parent from another tenant resolves to null → rejected.
            var parent = await _repository.GetByIdAsync(parentId, cancellationToken);
            if (parent is null)
            {
                return Response<NoContent>.Fail("Parent Legal Entity was not found in this tenant.", 400);
            }

            // Cycle guard: the proposed parent must not be the entity itself or any of its descendants.
            var subtree = await _hierarchyResolver.GetSelfAndDescendantIdsAsync(entity.Id, cancellationToken);
            if (subtree is not null && subtree.Contains(parentId))
            {
                return Response<NoContent>.Fail("Assigning this parent would create a cycle in the legal-entity hierarchy.", 400);
            }
        }

        entity.ParentId = request.ParentId;
        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : Response<NoContent>.Fail("Legal Entity not found.", 404);
    }
}
