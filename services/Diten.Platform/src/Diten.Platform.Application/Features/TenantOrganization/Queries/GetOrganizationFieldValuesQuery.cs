using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Queries;

/// <summary>
/// Reads custom field values: one unit's values, or a filtered page across units.
///
/// <para>⚠ EVERY FILTER IS CHECKED BEFORE ANYTHING RUNS. A clause naming a definition that is not
/// <c>IsQueryable</c> makes the whole request a 400 — it is never dropped so the rest can proceed. Dropping it
/// would return a result set that is wrong and indistinguishable from a right one.</para>
/// </summary>
public sealed record GetOrganizationFieldValuesQuery(
    Guid? OrganizationUnitId = null,
    IReadOnlyList<OrganizationFieldValueFilterRequest>? Filters = null,
    Guid? SortDefinitionId = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 50) : IRequest<Response<OrganizationFieldValuePageDto>>;
