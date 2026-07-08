using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Queries;

public sealed record SearchPersonReferencesQuery(
    string? Query,
    string? Status,
    int Page,
    int PageSize) : IRequest<Response<PersonReferenceSearchResultDto>>;
