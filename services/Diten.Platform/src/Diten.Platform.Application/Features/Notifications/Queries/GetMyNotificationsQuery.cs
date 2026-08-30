using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

/// <summary>
/// BL-025 — the caller's own notifications, one page.
///
/// <para><b>⚠ THIS RECORD CARRIES NO IDENTITY, AND THAT IS THE WHOLE DESIGN.</b> Compare
/// <c>GetNotificationDispatchListQuery</c>, which opens with <c>Guid TenantId</c>: that query serves a
/// platform operator CHOOSING a tenant to inspect, so the subject is a genuine input. Here the subject is the
/// caller, and a caller does not get to nominate themselves. Tenant comes from <c>ITenantContext</c> and user
/// from <c>ICurrentUserContext</c>, both filled from the validated token.</para>
///
/// <para>The absence is load-bearing rather than stylistic: a <c>UserId</c> parameter on this record would be
/// bindable from the query string the moment anyone added it to the controller signature, and "read anyone's
/// notifications by guessing a guid" would be one plausible-looking edit away.
/// <c>UserNotificationTests</c> pins the absence by reflection so that edit cannot land quietly.</para>
///
/// <para>Paging IS an input — it says nothing about whose data is returned.</para>
/// </summary>
public sealed record GetMyNotificationsQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<Response<UserNotificationPageDto>>;
