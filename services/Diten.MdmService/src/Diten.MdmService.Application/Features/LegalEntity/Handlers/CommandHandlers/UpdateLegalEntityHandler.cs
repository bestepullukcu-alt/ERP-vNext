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
        ArgumentNullException.ThrowIfNull(request);

        if (request.Request.ExpectedVersion is not { } expectedVersion || expectedVersion < 0)
        {
            return Response<NoContent>.Fail("ExpectedVersion must be a non-negative integer.", 400);
        }

        var entity = await _repository.GetByIdAsync(request.LegalEntityId, cancellationToken);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Legal Entity not found.", 404);
        }

        if (entity.Version != expectedVersion)
        {
            return Response<NoContent>.Fail("The Legal Entity has changed since it was loaded.", 409);
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

        var proposed = new Domain.Entities.LegalEntity
        {
            Id = entity.Id,
            TenantId = entity.TenantId
        };
        LegalEntityMappings.ApplyEditableFields(proposed, r);

        var updated = await _repository.UpdateEditableFieldsAsync(proposed, expectedVersion, cancellationToken);
        if (updated)
        {
            return Response<NoContent>.SuccessWithoutData(204);
        }

        // Preserve non-disclosure for targets that vanished (including delete races). A still-visible target proves
        // the qualified CAS lost to a version change and is therefore a stale edit conflict.
        var stillVisible = await _repository.GetByIdAsync(request.LegalEntityId, cancellationToken);
        return stillVisible is null
            ? Response<NoContent>.Fail("Legal Entity not found.", 404)
            : Response<NoContent>.Fail("The Legal Entity has changed since it was loaded.", 409);
    }
}
