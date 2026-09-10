using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

/// <summary>
/// Writes ONE unit's value for ONE definition. Insert, update and clear all arrive here, because from the
/// caller's side they are one intention — "this unit's Regulatory role is now X, or is now nothing".
/// </summary>
public sealed class SetOrganizationFieldValueCommandHandler
    : IRequestHandler<SetOrganizationFieldValueCommand, Response<Guid>>
{
    private readonly IOrganizationUnitRepository _units;
    private readonly IOrganizationFieldDefinitionRepository _definitions;
    private readonly IOrganizationFieldValueRepository _values;
    private readonly IPositionRepository _positions;
    private readonly ITenantContext _tenantContext;

    public SetOrganizationFieldValueCommandHandler(
        IOrganizationUnitRepository units,
        IOrganizationFieldDefinitionRepository definitions,
        IOrganizationFieldValueRepository values,
        IPositionRepository positions,
        ITenantContext tenantContext)
    {
        _units = units;
        _definitions = definitions;
        _values = values;
        _positions = positions;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(SetOrganizationFieldValueCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        /*
         * ⚠ BOTH REFERENCES ARE READ THROUGH THE TENANT-SCOPED REPOSITORIES, so a unit or definition belonging
         * to another tenant simply is not found. The 404 is therefore non-disclosing by construction rather
         * than by a rule someone has to remember to apply.
         */
        var unit = await _units.GetByIdAsync(request.OrganizationUnitId, ct);
        if (unit is null || unit.IsArchived)
        {
            return Response<Guid>.Fail("Organization Unit not found.", 404);
        }

        var definition = await _definitions.GetByIdAsync(request.Request.DefinitionId, ct);
        if (definition is null || definition.DeletedAt is not null)
        {
            return Response<Guid>.Fail("Organization Unit field definition not found.", 404);
        }

        var existing = await _values.GetAsync(unit.Id, definition.Id, ct);

        if (!definition.IsActive)
        {
            // Retired definitions accept no NEW writes; what is already stored stays readable (§12).
            return Response<Guid>.Fail("This field definition is retired and accepts no new values.", 409);
        }

        var (canonical, message, status) =
            OrganizationFieldDefinitionRules.CanonicalizeValue(definition, request.Request.Value);
        if (message is not null)
        {
            return Response<Guid>.Fail(message, status);
        }

        if (canonical is not null && definition.DataType == OrganizationFieldDataType.Reference)
        {
            var referenceError = await ValidateReferenceAsync(definition, canonical, ct);
            if (referenceError is not null)
            {
                return Response<Guid>.Fail(referenceError, 404);
            }
        }

        // ── clearing ────────────────────────────────────────────────────────────────────────────────────
        if (canonical is null)
        {
            if (existing is null)
            {
                return Response<Guid>.Success(Guid.Empty, 204);
            }

            return await _values.TrySoftDeleteAsync(existing.Id, request.Request.ExpectedVersion ?? existing.Version, ct)
                ? Response<Guid>.Success(existing.Id, 204)
                : Stale<Guid>();
        }

        // ── updating an existing value ──────────────────────────────────────────────────────────────────
        if (existing is not null)
        {
            existing.Value = canonical;
            existing.ValueType = definition.DataType;
            existing.Classification = definition.Classification;

            return await _values.TryUpdateAsync(existing, request.Request.ExpectedVersion ?? existing.Version, ct)
                ? Response<Guid>.Success(existing.Id, 200)
                : Stale<Guid>();
        }

        // ── first value for this unit+definition ────────────────────────────────────────────────────────
        var entity = new OrganizationFieldValue
        {
            TenantId = tenantId,
            OrganizationUnitId = unit.Id,
            DefinitionId = definition.Id,
            ValueType = definition.DataType,
            Value = canonical,
            Classification = definition.Classification
        };

        /*
         * ⚠ THE INSERT MAY LOSE, AND THAT IS THE POINT. "One active value per unit per definition" is a UNIQUE
         * PARTIAL INDEX in the database, not the `existing is null` check above — that check is a read, and a
         * second writer can pass it between our read and our write. When the index rejects the duplicate the
         * loser gets 409 rather than a second row nobody notices.
         */
        return await _values.TryInsertAsync(entity, ct)
            ? Response<Guid>.Success(entity.Id, 201)
            : Response<Guid>.Fail(
                "Another value for this field was recorded concurrently. Re-read it and try again.", 409);
    }

    private async Task<string?> ValidateReferenceAsync(
        OrganizationFieldDefinition definition,
        string canonical,
        CancellationToken ct)
    {
        var target = definition.ValidationRules?.ReferenceTarget;
        var id = Guid.Parse(canonical);

        return target switch
        {
            OrganizationFieldReferenceTarget.OrganizationUnit =>
                await _units.GetByIdAsync(id, ct) is null ? "Referenced Organization Unit not found." : null,
            OrganizationFieldReferenceTarget.Position =>
                await _positions.GetByIdAsync(id, ct) is null ? "Referenced Position not found." : null,
            _ => "Referenced record not found."
        };
    }

    private static Response<T> Stale<T>() => Response<T>.Fail(
        "This value changed since it was read. Re-read it and try again.", 409);
}
