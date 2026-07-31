namespace Diten.Platform.Application.Contracts;

/// <summary>
/// A person a task notification can actually be delivered to (WC-4).
///
/// <para><c>Email</c> is the deliverable address. <c>DisplayName</c> is best-effort and only ever decorates the
/// envelope — a notification with a name and no address cannot be sent, and one with an address and no name
/// can.</para>
/// </summary>
public sealed record TaskNotificationRecipient(Guid UserId, string Email, string? DisplayName);

/// <summary>
/// WC-4 — turns user IDs into people a message can reach.
///
/// <para><b>Why this seam exists.</b> MOD-0024 holds no user directory: it knows who a task belongs to as a GUID
/// and nothing more. Before this interface, that GUID was written straight into the recipient's email field —
/// the code said so in a comment — so every task notification was addressed to
/// <c>3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234</c> and could never be delivered. The address lives in AuthService,
/// and this is the one place that knows it.</para>
///
/// <para><b>Never throws, and resolves best-effort.</b> A recipient who cannot be resolved is OMITTED from the
/// result rather than represented by a placeholder — the caller then knows exactly who it can reach, and a
/// notification is never addressed to something that is not an address. AuthService being unreachable therefore
/// degrades to "nobody was notified", which is a logged, visible outcome; it must never fail the task write that
/// triggered it.</para>
/// </summary>
public interface ITaskNotificationRecipientResolver
{
    /// <summary>
    /// The subset of <paramref name="userIds"/> that can actually be reached. Ids with no resolvable address are
    /// absent from the result — the count difference IS the "some could not be resolved" signal.
    /// </summary>
    Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default);
}
