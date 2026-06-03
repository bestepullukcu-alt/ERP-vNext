using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

internal static class OrganizationUnitCycleGuard
{
    public static async Task<Response<NoContent>> EnsureNoCycleAsync(
        IOrganizationUnitRepository repository,
        Guid? currentId,
        Guid parentId,
        CancellationToken ct)
    {
        if (!currentId.HasValue)
        {
            return Response<NoContent>.Success(204);
        }

        var visited = new HashSet<Guid>();
        var cursor = parentId;
        for (var depth = 0; depth < 32; depth++)
        {
            if (!visited.Add(cursor))
            {
                return Response<NoContent>.Fail("Organization Unit parent cycle detected.", 409);
            }

            if (cursor == currentId.Value)
            {
                return Response<NoContent>.Fail("Organization Unit parent cycle detected.", 409);
            }

            var node = await repository.GetByIdAsync(cursor, ct);
            if (node?.ParentOrganizationUnitId == null)
            {
                return Response<NoContent>.Success(204);
            }

            cursor = node.ParentOrganizationUnitId.Value;
        }

        return Response<NoContent>.Fail("Organization Unit parent chain exceeded maximum depth.", 409);
    }
}
