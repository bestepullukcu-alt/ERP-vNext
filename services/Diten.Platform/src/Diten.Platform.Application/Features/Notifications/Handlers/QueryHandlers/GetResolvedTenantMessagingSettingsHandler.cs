using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Application.Features.Notifications.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

public sealed class GetResolvedTenantMessagingSettingsHandler
    : IRequestHandler<GetResolvedTenantMessagingSettingsQuery, Response<ResolvedMessagingSettingsDto>>
{
    private readonly ITenantMessagingSettingsResolver _resolver;

    public GetResolvedTenantMessagingSettingsHandler(ITenantMessagingSettingsResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<Response<ResolvedMessagingSettingsDto>> Handle(GetResolvedTenantMessagingSettingsQuery request, CancellationToken ct) =>
        await _resolver.ResolveAsync(request.TenantId, ct);
}
