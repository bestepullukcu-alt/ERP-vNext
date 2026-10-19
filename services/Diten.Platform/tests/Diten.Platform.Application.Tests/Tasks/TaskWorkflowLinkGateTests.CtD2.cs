using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2-FIX5 CT acceptance — the link of a round that could not be withdrawn is written only where
/// the task names nothing yet. A task that already names a round (a newer one started meanwhile) keeps it: writing the older
/// round over it would point the read model at a round that is not the live one.
/// </summary>
public sealed partial class TaskWorkflowLinkGateTests
{
    [Fact]
    public async Task A_link_that_cannot_be_withdrawn_is_not_written_over_a_newer_round_the_task_already_names()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        f.SeedDroppedReviewRequirementOnly();
        var log = new CapturingLogger<SubmitTaskForReviewHandler>();
        var newer = Guid.NewGuid();
        f.Engine.Down = true;                                         // the withdraw of the started round is refused
        f.Engine.AfterStart = () =>
        {
            f.RivalWrites(RequirementOff, review: true);
            f.HoldLink(review: true, newer);                          // … and the task already names another round
        };

        var lost = await f.SubmitAsAsync(TaskTestData.Me, f.Reviews, log);

        Assert.Equal(409, lost.StatusCode);
        var round = Assert.Single(f.Engine.Started);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(round).Status);   // non-vacuity: the withdraw did fail
        Assert.Equal(newer, f.Stored().ReviewWorkflowInstanceId);                          // the held link is kept, not overwritten
        Assert.Contains(log.Entries, e => e.Message.Contains(newer.ToString(), StringComparison.Ordinal)
                                          && e.Message.Contains(round.Id.ToString(), StringComparison.Ordinal));
    }

    private sealed partial class Fixture
    {
        /// <summary>The task names <paramref name="instanceId"/> as its review (or approval) round, without moving its version.</summary>
        public void HoldLink(bool review, Guid instanceId)
        {
            var stored = Raw();
            if (review)
            {
                stored.ReviewWorkflowInstanceId = instanceId;
            }
            else
            {
                stored.WorkflowInstanceId = instanceId;
            }
        }
    }
}
