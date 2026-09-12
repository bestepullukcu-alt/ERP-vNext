using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class UpdateOrganizationFieldDefinitionCommandHandler
    : IRequestHandler<UpdateOrganizationFieldDefinitionCommand, Response<NoContent>>
{
    private readonly IOrganizationFieldDefinitionRepository _definitions;
    private readonly IOrganizationFieldValueRepository _values;

    public UpdateOrganizationFieldDefinitionCommandHandler(
        IOrganizationFieldDefinitionRepository definitions,
        IOrganizationFieldValueRepository values)
    {
        _definitions = definitions;
        _values = values;
    }

    public async Task<Response<NoContent>> Handle(UpdateOrganizationFieldDefinitionCommand request, CancellationToken ct)
    {
        var r = request.Request;
        var entity = await _definitions.GetByIdAsync(request.Id, ct);
        if (entity is null || entity.DeletedAt is not null)
        {
            // Non-disclosing: a cross-tenant id and a missing one are answered identically.
            return Response<NoContent>.Fail("Organization Unit field definition not found.", 404);
        }

        if (!OrganizationFieldDefinitionRules.TryParseDataType(r.DataType, out var dataType))
        {
            return Response<NoContent>.Fail($"'{r.DataType}' is not a supported field data type.", 400);
        }

        /*
         * ⚠ THE TYPE MAY ONLY MOVE WHILE THE DEFINITION IS STILL EMPTY. Once a value exists, re-typing the
         * definition would re-interpret data already recorded — "2026-01-02" read as text one day and as a
         * date the next — and nothing would fail to announce it. Refused rather than migrated: a migration
         * that guesses is worse than a refusal that is explicit.
         */
        if (dataType != entity.DataType && await _values.AnyForDefinitionAsync(entity.Id, ct))
        {
            return Response<NoContent>.Fail(
                "This field's type cannot change while values are stored against it.", 409);
        }

        if (!OrganizationFieldDefinitionRules.TryParseClassification(r.Classification, out var classification))
        {
            return Response<NoContent>.Fail($"'{r.Classification}' is not a supported classification.", 400);
        }

        var constraints = CreateOrganizationFieldDefinitionCommandHandler.MapConstraints(r.ValidationRules, out var constraintError);
        if (constraintError is not null)
        {
            return Response<NoContent>.Fail(constraintError, 400);
        }

        if (OrganizationFieldDefinitionRules.ValidateConstraints(dataType, constraints) is { } shapeError)
        {
            return Response<NoContent>.Fail(shapeError.Message, shapeError.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(r.Name))
        {
            return Response<NoContent>.Fail("A field definition needs a name.", 400);
        }

        entity.Name = r.Name.Trim();
        entity.DataType = dataType;
        entity.IsRequired = r.IsRequired;
        entity.IsQueryable = r.IsQueryable;
        entity.DisplayOrder = r.DisplayOrder < 0 ? 0 : r.DisplayOrder;
        entity.Classification = classification;
        entity.ValidationRules = constraints;

        // Code is absent from the request by construction (§12 immutability); nothing here can change it.
        return await _definitions.TryUpdateAsync(entity, r.ExpectedVersion, ct)
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail(
                "This field definition changed since it was read. Re-read it and try again.", 409);
    }
}
