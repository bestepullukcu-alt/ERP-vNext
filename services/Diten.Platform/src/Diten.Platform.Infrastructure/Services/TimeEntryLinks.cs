using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// MOD-0280-FU01 T3 — the two pages a time-entry e-mail links to, on the web origin every other Platform mailer uses
/// (<see cref="AuthServiceOptions.FrontendBaseUrl"/>, the <c>MeetingInviteMailer</c> precedent). Here rather than in
/// Application because that setting lives here.
/// </summary>
public sealed class TimeEntryLinks : ITimeEntryLinks
{
    private readonly string _origin;

    public TimeEntryLinks(IOptions<AuthServiceOptions> options)
        => _origin = (options.Value.FrontendBaseUrl ?? string.Empty).TrimEnd('/');

    public string MyWeek(string weekKey) => $"{_origin}/TimeEntry?week={Uri.EscapeDataString(weekKey)}";

    public string ApprovalWeek(Guid weekId) => $"{_origin}/TimeEntry/Approvals/{weekId:D}";
}
