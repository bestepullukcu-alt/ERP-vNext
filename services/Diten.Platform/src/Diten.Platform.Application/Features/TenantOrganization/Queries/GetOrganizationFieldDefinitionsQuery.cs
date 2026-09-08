using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Queries;

/// <summary>
/// The tenant's field definitions, ordered by <c>DisplayOrder</c> then code.
/// <paramref name="IncludeInactive"/> defaults to false so an ordinary surface never renders a retired field.
/// </summary>
public sealed record GetOrganizationFieldDefinitionsQuery(bool IncludeInactive = false)
    : IRequest<Response<IReadOnlyList<OrganizationFieldDefinitionDto>>>;
