using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Workflow.Services;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 D2-FIX4 item 3 — an SLA rule that is added, tightened or replaced is seen by the tasks that were
/// resting under the old one.
///
/// <para><b>Why it is needed.</b> The sweep rests every overdue task it cannot act on until its look time — a day, for a task
/// that has no SLA rule (D2-FIX3 item 4). A rule written afterwards changes the answer for exactly those tasks, but their look
/// times were already stored, so the new rule took effect only when each one's day was over: an escalation the operator had
/// just configured waited up to twenty-four hours. Writing a rule therefore forgets the look time of the template's open
/// tasks, and the next sweep reads them with the new rule in hand.</para>
///
/// <para><b>What it touches.</b> Only <c>EscalationNextLookAtUtc</c> (a targeted write, tenant-filtered, no version), only tasks
/// still waiting on somebody, only the instances of the one template. Nothing is escalated or timed out here — that stays the
/// sweep's decision, on the server clock.</para>
///
/// <para><b>Best effort, said out loud.</b> The rule is already saved when this runs, so a failure here cannot undo it; it is
/// logged (never swallowed silently) and the old behaviour — the tasks look again when their rest is over — is the worst case.
/// Without the two repositories (a handler built by a test that predates this) nothing is reset.</para>
/// </summary>
internal static class WorkflowSlaLookReset
{
    public static async Task ForTemplateAsync(
        Guid templateId,
        IWorkflowInstanceRepository? instances,
        IApprovalTaskRepository? tasks,
        ILogger logger,
        CancellationToken ct)
    {
        if (instances is null || tasks is null)
        {
            return;
        }

        try
        {
            var openInstances = await instances.ListOpenIdsByTemplateAsync(templateId, ct);
            if (openInstances.Count == 0)
            {
                return;
            }

            var reset = await tasks.ResetEscalationLooksAsync(openInstances, ct);
            logger.LogInformation(
                "SLA rules of workflow template {TemplateId} changed: {Reset} resting task(s) will be looked at by the next sweep.",
                templateId, reset);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogWarning(
                "SLA rules of workflow template {TemplateId} changed, but the resting tasks could not be reset ({ExceptionType}); "
                + "they are looked at again when their rest is over.",
                templateId, failure.GetType().Name);
        }
    }
}
