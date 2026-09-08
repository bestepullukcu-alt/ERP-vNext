using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class CreateOrganizationFieldDefinitionCommandHandler
    : IRequestHandler<CreateOrganizationFieldDefinitionCommand, Response<Guid>>
{
    private readonly IOrganizationFieldDefinitionRepository _definitions;
    private readonly ITenantContext _tenantContext;

    public CreateOrganizationFieldDefinitionCommandHandler(
        IOrganizationFieldDefinitionRepository definitions,
        ITenantContext tenantContext)
    {
        _definitions = definitions;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateOrganizationFieldDefinitionCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var r = request.Request;

        var code = OrganizationFieldDefinitionRules.NormalizeCode(r.Code);
        if (OrganizationFieldDefinitionRules.ValidateCode(code) is { } codeError)
        {
            return Response<Guid>.Fail(codeError.Message, codeError.StatusCode);
        }

        if (!OrganizationFieldDefinitionRules.TryParseDataType(r.DataType, out var dataType))
        {
            return Response<Guid>.Fail(
                $"'{r.DataType}' is not a supported field data type.", 400);
        }

        if (!OrganizationFieldDefinitionRules.TryParseClassification(r.Classification, out var classification))
        {
            return Response<Guid>.Fail($"'{r.Classification}' is not a supported classification.", 400);
        }

        var constraints = MapConstraints(r.ValidationRules, out var constraintError);
        if (constraintError is not null)
        {
            return Response<Guid>.Fail(constraintError, 400);
        }

        if (OrganizationFieldDefinitionRules.ValidateConstraints(dataType, constraints) is { } shapeError)
        {
            return Response<Guid>.Fail(shapeError.Message, shapeError.StatusCode);
        }

        var existing = await _definitions.GetAllAsync(ct);

        /*
         * The unique partial index on (TenantId, Code) is the real enforcement — this read only turns the
         * common case into a readable 409 instead of a duplicate-key error. A code that slips past it because
         * of a concurrent create still fails at the insert, which is why the repository reports it.
         */
        if (existing.Any(d => d.DeletedAt is null && string.Equals(d.Code, code, StringComparison.Ordinal)))
        {
            return Response<Guid>.Fail("A field definition with this code already exists.", 409);
        }

        if (OrganizationFieldDefinitionRules.ValidateDefinitionCount(existing) is { } limitError)
        {
            return Response<Guid>.Fail(limitError.Message, limitError.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(r.Name))
        {
            return Response<Guid>.Fail("A field definition needs a name.", 400);
        }

        var entity = new OrganizationFieldDefinition
        {
            TenantId = tenantId,
            Code = code,
            Name = r.Name.Trim(),
            DataType = dataType,
            IsRequired = r.IsRequired,
            IsQueryable = r.IsQueryable,
            IsActive = true,
            DisplayOrder = r.DisplayOrder < 0 ? 0 : r.DisplayOrder,
            Classification = classification,
            ValidationRules = constraints
        };

        await _definitions.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    internal static OrganizationFieldConstraints? MapConstraints(
        OrganizationFieldConstraintsRequest? request,
        out string? error)
    {
        error = null;
        if (request is null)
        {
            return null;
        }

        OrganizationFieldReferenceTarget? target = null;
        if (!string.IsNullOrWhiteSpace(request.ReferenceTarget))
        {
            if (!OrganizationFieldDefinitionRules.TryParseReferenceTarget(request.ReferenceTarget, out var parsed))
            {
                error = $"'{request.ReferenceTarget}' is not a supported reference target.";
                return null;
            }

            target = parsed;
        }

        return new OrganizationFieldConstraints
        {
            MinLength = request.MinLength,
            MaxLength = request.MaxLength,
            MinValue = request.MinValue,
            MaxValue = request.MaxValue,
            Options = request.Options?.Select(o => o?.Trim() ?? string.Empty).ToList(),
            ReferenceTarget = target
        };
    }
}
