using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

public sealed class GetTenantMessagingSettingsHandler
    : IRequestHandler<GetTenantMessagingSettingsQuery, Response<TenantMessagingSettingsDto>>
{
    private readonly ITenantMessagingSettingsRepository _repository;

    public GetTenantMessagingSettingsHandler(ITenantMessagingSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<TenantMessagingSettingsDto>> Handle(GetTenantMessagingSettingsQuery request, CancellationToken ct)
    {
        var settings = await _repository.GetByTenantIdAsync(request.TenantId, ct);
        return settings is null
            ? Response<TenantMessagingSettingsDto>.Fail("Tenant messaging settings not found.", 404)
            : Response<TenantMessagingSettingsDto>.Success(settings.ToDto());
    }
}
