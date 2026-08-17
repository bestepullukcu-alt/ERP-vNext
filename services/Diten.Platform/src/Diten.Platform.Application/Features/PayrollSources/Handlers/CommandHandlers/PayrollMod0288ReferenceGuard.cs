using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

internal static class PayrollMod0288ReferenceGuard
{
    public static async Task<Response<NoContent>> ValidateAsync(
        PayrollEmployeeReferenceMapRequest request,
        IOrganizationUnitRepository organizationUnits,
        IPositionRepository positions,
        CancellationToken ct)
    {
        if (request.MappingState == PayrollReferenceMappingState.Mapped
            && !request.PersonReferenceId.HasValue
            && !request.OrganizationUnitReferenceId.HasValue
            && !request.PositionReferenceId.HasValue)
        {
            return Response<NoContent>.Fail("Mapped payroll reference requires a same-tenant MOD-0288 reference.", 409);
        }

        if (request.PersonReferenceId.HasValue)
        {
            return Response<NoContent>.Fail("MOD-0288 Person reference validation is not available in this service.", 404);
        }

        if (request.OrganizationUnitReferenceId.HasValue
            && await organizationUnits.GetByIdAsync(request.OrganizationUnitReferenceId.Value, ct) == null)
        {
            return Response<NoContent>.Fail("MOD-0288 OrganizationUnit reference not found for current tenant.", 404);
        }

        if (request.PositionReferenceId.HasValue
            && await positions.GetByIdAsync(request.PositionReferenceId.Value, ct) == null)
        {
            return Response<NoContent>.Fail("MOD-0288 Position reference not found for current tenant.", 404);
        }

        return Response<NoContent>.Success(204);
    }
}
