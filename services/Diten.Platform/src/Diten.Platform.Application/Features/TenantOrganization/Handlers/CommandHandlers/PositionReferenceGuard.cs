using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

internal static class PositionReferenceGuard
{
    public static async Task<Response<NoContent>> ValidateAsync(
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        Guid? currentId,
        Guid organizationUnitId,
        Guid? reportsToPositionId,
        CancellationToken ct)
    {
        var organizationUnit = await organizationUnits.GetByIdAsync(organizationUnitId, ct);
        if (organizationUnit == null || organizationUnit.IsArchived)
        {
            return Response<NoContent>.Fail("Organization Unit not found.", 404);
        }

        if (!reportsToPositionId.HasValue)
        {
            return Response<NoContent>.Success(204);
        }

        if (currentId.HasValue && currentId.Value == reportsToPositionId.Value)
        {
            return Response<NoContent>.Fail("Position cannot report to itself.", 409);
        }

        var manager = await positions.GetByIdAsync(reportsToPositionId.Value, ct);
        if (manager == null || manager.IsArchived)
        {
            return Response<NoContent>.Fail("Reports-to Position not found.", 404);
        }

        return await EnsureNoReportingCycleAsync(positions, currentId, reportsToPositionId.Value, ct);
    }

    private static async Task<Response<NoContent>> EnsureNoReportingCycleAsync(
        IPositionRepository positions,
        Guid? currentId,
        Guid reportsToPositionId,
        CancellationToken ct)
    {
        if (!currentId.HasValue)
        {
            return Response<NoContent>.Success(204);
        }

        var visited = new HashSet<Guid>();
        var cursor = reportsToPositionId;
        for (var depth = 0; depth < 32; depth++)
        {
            if (!visited.Add(cursor) || cursor == currentId.Value)
            {
                return Response<NoContent>.Fail("Position reporting cycle detected.", 409);
            }

            var node = await positions.GetByIdAsync(cursor, ct);
            if (node?.ReportsToPositionId == null)
            {
                return Response<NoContent>.Success(204);
            }

            cursor = node.ReportsToPositionId.Value;
        }

        return Response<NoContent>.Fail("Position reporting chain exceeded maximum depth.", 409);
    }
}
