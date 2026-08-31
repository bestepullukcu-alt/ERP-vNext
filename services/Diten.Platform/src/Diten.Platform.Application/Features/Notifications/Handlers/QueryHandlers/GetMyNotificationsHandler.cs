using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

/// <summary>
/// BL-025 — one page of the CALLER's notifications.
///
/// <para><b>The scope is assembled here, from contexts, and nowhere else.</b> Tenant from
/// <see cref="ITenantContext"/> (the resolution middleware's answer) and user from
/// <see cref="ICurrentUserContext"/> (the token's <c>sub</c> claim). The query record carries neither, so
/// there is no request shape — route, query string, body or header — in which a caller can name a subject.
/// That is not defence in depth on top of a permission check; it IS the check, and it is the only one that
/// cannot be misconfigured by an administrator.</para>
/// </summary>
public sealed class GetMyNotificationsHandler
    : IRequestHandler<GetMyNotificationsQuery, Response<UserNotificationPageDto>>
{
    private const int MaxPageSize = 100;

    private readonly IUserNotificationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public GetMyNotificationsHandler(
        IUserNotificationRepository repository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<UserNotificationPageDto>> Handle(
        GetMyNotificationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
        {
            /*
             * No subject means no inbox — never "everybody's inbox". A token that carries no usable `sub` is
             * an authentication problem, and answering it with rows would be the worst possible reading of an
             * empty filter.
             */
            return Response<UserNotificationPageDto>.Fail(
                "The caller has no resolvable user identity.", 401);
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var tenantId = _tenantContext.TenantId;

        var items = await _repository.ListForUserAsync(
            tenantId, userId, (page - 1) * pageSize, pageSize, ct);

        var unread = await _repository.CountUnreadForUserAsync(tenantId, userId, ct);

        return Response<UserNotificationPageDto>.Success(
            new UserNotificationPageDto(
                items.Select(x => x.ToDto()).ToArray(),
                unread,
                page,
                pageSize));
    }
}
