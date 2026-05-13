using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PlatformAdministrators.Queries;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.QueryHandlers;

public sealed class GetPlatformAdministratorsHandler
    : IRequestHandler<GetPlatformAdministratorsQuery, Response<PagedResult<PlatformAdministratorListItemDto>>>
{
    private readonly IPlatformAdministratorRepository _repository;

    public GetPlatformAdministratorsHandler(IPlatformAdministratorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PagedResult<PlatformAdministratorListItemDto>>> Handle(
        GetPlatformAdministratorsQuery request,
        CancellationToken ct)
    {
        Guid? partnerId = null;
        if (Guid.TryParse(request.Filter.PartnerId, out var parsedPartnerId))
        {
            partnerId = parsedPartnerId;
        }

        var query = new PlatformAdministratorQuery(
            request.Filter.Search,
            PlatformAdministratorParsing.SplitEnums<AdministratorStatus>(request.Filter.Status),
            PlatformAdministratorParsing.SplitEnums<ActorType>(request.Filter.ActorType),
            PlatformAdministratorParsing.SplitEnums<AdministratorRole>(request.Filter.Role),
            PlatformAdministratorParsing.SplitEnums<AdministratorInvitationStatus>(request.Filter.InvitationStatus),
            partnerId,
            request.Filter.Page,
            request.Filter.PageSize,
            request.Filter.Sort);

        var (items, totalCount) = await _repository.QueryAsync(query, ct);
        return Response<PagedResult<PlatformAdministratorListItemDto>>.Success(
            PlatformAdministratorMapper.ToPagedResult(items, request.Filter.Page, request.Filter.PageSize, totalCount));
    }
}
