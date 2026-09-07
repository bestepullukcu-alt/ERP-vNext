using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetOrganizationFieldDefinitionsQueryHandler
    : IRequestHandler<GetOrganizationFieldDefinitionsQuery, Response<IReadOnlyList<OrganizationFieldDefinitionDto>>>
{
    private readonly IOrganizationFieldDefinitionRepository _definitions;

    public GetOrganizationFieldDefinitionsQueryHandler(IOrganizationFieldDefinitionRepository definitions)
        => _definitions = definitions;

    public async Task<Response<IReadOnlyList<OrganizationFieldDefinitionDto>>> Handle(
        GetOrganizationFieldDefinitionsQuery request,
        CancellationToken ct)
    {
        var items = await _definitions.GetAllAsync(ct);

        IReadOnlyList<OrganizationFieldDefinitionDto> result = items
            .Where(d => d.DeletedAt is null && (request.IncludeInactive || d.IsActive))
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Code, StringComparer.Ordinal)
            .Select(OrganizationFieldMapper.ToDto)
            .ToList();

        return Response<IReadOnlyList<OrganizationFieldDefinitionDto>>.Success(result);
    }
}
