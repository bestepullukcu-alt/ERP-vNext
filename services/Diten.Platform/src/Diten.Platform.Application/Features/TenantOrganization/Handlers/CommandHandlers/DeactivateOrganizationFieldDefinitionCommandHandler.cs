using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class DeactivateOrganizationFieldDefinitionCommandHandler
    : IRequestHandler<DeactivateOrganizationFieldDefinitionCommand, Response<NoContent>>
{
    private readonly IOrganizationFieldDefinitionRepository _definitions;

    public DeactivateOrganizationFieldDefinitionCommandHandler(IOrganizationFieldDefinitionRepository definitions)
        => _definitions = definitions;

    public async Task<Response<NoContent>> Handle(
        DeactivateOrganizationFieldDefinitionCommand request,
        CancellationToken ct)
    {
        var entity = await _definitions.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.DeletedAt is not null)
        {
            return Response<NoContent>.Fail("Organization Unit field definition not found.", 404);
        }

        if (!entity.IsActive)
        {
            // Idempotent, and it says so: a second deactivation is not an error, but it is not a change either.
            return Response<NoContent>.Success(204);
        }

        entity.IsActive = false;

        /*
         * ⚠ NOT A DELETE. Values already recorded against this definition stay exactly where they are and stay
         * readable; only new writes are refused (§12). The definition's code also stays occupied until it is
         * soft-deleted — the unique index is partial on non-deleted rows, so retiring a field does not free
         * its code, and that is deliberate: reusing a retired code would silently re-label historical values.
         */
        return await _definitions.TryUpdateAsync(entity, request.ExpectedVersion, ct)
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail(
                "This field definition changed since it was read. Re-read it and try again.", 409);
    }
}
