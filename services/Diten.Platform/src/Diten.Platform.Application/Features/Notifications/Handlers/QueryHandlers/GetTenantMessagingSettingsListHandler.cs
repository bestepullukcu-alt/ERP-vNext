using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

public sealed class GetTenantMessagingSettingsListHandler
    : IRequestHandler<GetTenantMessagingSettingsListQuery, Response<IReadOnlyList<TenantMessagingSettingsDto>>>
{
    private readonly ITenantMessagingSettingsRepository _repository;

    public GetTenantMessagingSettingsListHandler(ITenantMessagingSettingsRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<TenantMessagingSettingsDto>>> Handle(GetTenantMessagingSettingsListQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await _repository.ListTenantSettingsAsync((page - 1) * pageSize, pageSize, ct);
        return Response<IReadOnlyList<TenantMessagingSettingsDto>>.Success(items.Select(x => x.ToDto()).ToArray());
    }
}
