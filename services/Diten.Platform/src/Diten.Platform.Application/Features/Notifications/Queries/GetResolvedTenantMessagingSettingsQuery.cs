using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

public sealed record GetResolvedTenantMessagingSettingsQuery(Guid TenantId) : IRequest<Response<ResolvedMessagingSettingsDto>>;
