using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// BL-454 — a task's page on the web origin every other Platform mailer uses
/// (<see cref="AuthServiceOptions.FrontendBaseUrl"/>; <c>TimeEntryLinks</c> and <c>MeetingInviteMailer</c> read the
/// same setting). No origin configured: an empty string, and the shell then draws no button — never a relative or
/// a guessed address in an e-mail.
/// </summary>
public sealed class TaskWebLinks : ITaskWebLinks
{
    private readonly string _origin;

    public TaskWebLinks(IOptions<AuthServiceOptions> options)
        => _origin = (options.Value.FrontendBaseUrl ?? string.Empty).Trim().TrimEnd('/');

    public string Detail(Guid taskId) => _origin.Length == 0 ? string.Empty : _origin + TaskLinks.Detail(taskId);
}
