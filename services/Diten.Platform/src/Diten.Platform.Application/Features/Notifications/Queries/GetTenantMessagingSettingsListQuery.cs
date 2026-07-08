using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

public sealed record GetTenantMessagingSettingsListQuery(int Page = 1, int PageSize = 50)
    : IRequest<Response<IReadOnlyList<TenantMessagingSettingsDto>>>;
