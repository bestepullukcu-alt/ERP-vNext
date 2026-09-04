using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

/// <summary>
/// Mark ONE of the caller's notifications read.
///
/// <para><b>Somebody else's id answers 200 with nothing marked, not 404.</b> The two are deliberately the
/// same answer: a 404 for "does not exist" and a 403 for "not yours" would together turn this endpoint into a
/// probe that confirms a given notification id belongs to somebody. Nothing useful is lost — a caller acting
/// on their own list can only ever send ids that are on it.</para>
/// </summary>
public sealed class MarkMyNotificationReadHandler
    : IRequestHandler<MarkMyNotificationReadCommand, Response<UserNotificationReadResultDto>>
{
    private readonly IUserNotificationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public MarkMyNotificationReadHandler(
        IUserNotificationRepository repository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<UserNotificationReadResultDto>> Handle(
        MarkMyNotificationReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
        {
            return Response<UserNotificationReadResultDto>.Fail(
                "The caller has no resolvable user identity.", 401);
        }

        var marked = await _repository.MarkReadAsync(
            _tenantContext.TenantId, userId, request.NotificationId, DateTimeOffset.UtcNow, ct);

        return Response<UserNotificationReadResultDto>.Success(
            new UserNotificationReadResultDto(marked ? 1 : 0));
    }
}

/// <summary>
/// Mark every unread notification of the caller read.
///
/// <para>The returned count is what ACTUALLY changed, so "there was nothing to mark" is distinguishable from
/// "twelve items were cleared" — a bell that redraws on the answer needs the difference.</para>
/// </summary>
public sealed class MarkAllMyNotificationsReadHandler
    : IRequestHandler<MarkAllMyNotificationsReadCommand, Response<UserNotificationReadResultDto>>
{
    private readonly IUserNotificationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public MarkAllMyNotificationsReadHandler(
        IUserNotificationRepository repository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<UserNotificationReadResultDto>> Handle(
        MarkAllMyNotificationsReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
        {
            /*
             * ⚠ THE ONE PLACE AN EMPTY SUBJECT WOULD BE CATASTROPHIC. This command's filter has no id of its
             * own — it marks everything the scope matches. A Guid.Empty user id reaching the repository would
             * be a filter that matches nothing today and, on any future widening, everything.
             */
            return Response<UserNotificationReadResultDto>.Fail(
                "The caller has no resolvable user identity.", 401);
        }

        var marked = await _repository.MarkAllReadAsync(
            _tenantContext.TenantId, userId, DateTimeOffset.UtcNow, ct);

        return Response<UserNotificationReadResultDto>.Success(
            new UserNotificationReadResultDto(marked));
    }
}
