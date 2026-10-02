using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// WP-ROLES-CLOSE-01 — the production <see cref="RoleAuditRecorder"/> for handler unit tests (the shape of
/// <see cref="UserAuditForTests"/>): the local row goes to the test's own <see cref="IRbacAuditRecorder"/> fake, the
/// Platform edge to a recording forwarder that never touches a network.
/// </summary>
internal static class RoleAuditForTests
{
    public static RoleAuditRecorder Over(IRbacAuditRecorder local, IPlatformAuditForwarder? forwarder = null, ILogger<RoleAuditRecorder>? logger = null)
        => new(local, forwarder ?? new RecordingPlatformAuditForwarder(), logger ?? NullLogger<RoleAuditRecorder>.Instance);
}
