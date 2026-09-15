namespace Diten.Platform.Application.Features.Tasks;

/// <summary>
/// BL-414 — the ONE place a user-facing address for a single task is written.
///
/// <para><b>Two doors, two different questions.</b></para>
/// <list type="bullet">
/// <item><see cref="Detail"/> — the Task Center detail (<c>/WorkCenterNext/Details/{id}</c>). Where a reader is
/// SENT: an in-app notification, a meeting's related-task row. It opens for any reader the task read rule admits
/// (<c>ITaskReadAccessPolicy</c>, through <c>GET api/v1/work-items/{id}</c>), whether or not the task is on that
/// reader's own list.</item>
/// <item><see cref="Record"/> — the module's own record page (<c>/Tasks/{id}</c>). The way OUT of the Task Center
/// to the record ("Kaynak kayıtta aç", the row's Edit): the work item's <c>Source.DeepLink</c> (owner decision
/// 2026-09-02).</item>
/// </list>
///
/// <para><b>Why a builder and not two literals.</b> The two addresses differ by a path segment and mean different
/// things; a literal typed at a call site is how a notification ends up on the record page and a source door ends
/// up pointing back at the page it sits on. <c>TaskLinkGuardTests</c> fails on any new hard-coded task link outside
/// this file.</para>
/// </summary>
public static class TaskLinks
{
    /// <summary>The Task Center detail — where a reader is sent to a task.</summary>
    public static string Detail(Guid taskId) => $"/WorkCenterNext/Details/{taskId}";

    /// <summary>The module's record page — the way out of the Task Center to the record itself.</summary>
    public static string Record(Guid taskId) => $"/Tasks/{taskId}";
}
