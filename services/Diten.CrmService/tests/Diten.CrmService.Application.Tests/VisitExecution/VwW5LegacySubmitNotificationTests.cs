using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;
using Diten.CrmService.Application.Tests.VisitWorkspace;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W5 · W5-BE (K-N4) — the LEGACY submit path (SubmitVisitReportHandler) calls the same notifier: one report summary
/// for the rep's line manager after a successful submit, nothing for a refused one, nothing new for the same submit again,
/// and a Platform that is down never changes the submit's answer.
/// </summary>
public sealed partial class VisitExecutionW3Tests
{
    [Fact]
    public async Task W5_the_legacy_submit_sends_the_same_summary_once_and_never_fails_for_the_email()
    {
        var n = new W5Notifications(Now);
        var notifier = n.Notifier(_contacts, _accounts, _reports, _plans);
        var plan = Seed();
        var dto = await StartedAsync(plan);
        SubmitVisitReportHandler Submit() => new(TenantCtx(), new NullActorContext(), _reports, _plans, RepCaller(), _clock, null, null, _executions, notifier);
        var command = new SubmitVisitReportCommand(plan.Id, null, null, new VisitReportFeedbackInput("fine", "positive", false, null), null, null, null);

        // refused (the visit is not finished): no e-mail
        Assert.Equal(409, (await Submit().Handle(command, default)).StatusCode);
        Assert.Empty(n.Rows.Items);

        _clock.Value = Now.AddMinutes(25);
        await Capturer().Handle(new FinishVisitExecutionCommand(dto.Id, null), default);
        n.Platform.Down = true;
        var ok = await Submit().Handle(command, default);
        Assert.Equal(201, ok.StatusCode);                                                // the e-mail failing changes nothing
        var row = Assert.Single(n.Rows.Items);
        Assert.Equal((VisitNotificationEvents.ReportSubmitted, ok.Data, VisitNotificationStatuses.Failed), (row.EventCode, row.SubjectId, row.Status));
        Assert.Equal(Rep, Assert.Single(Assert.Single(n.Platform.Requests).To).LineManagerOf);

        // the same submit again (inside the window): no second row, no second request
        Assert.True((await Submit().Handle(command, default)).IsSuccessful);
        Assert.Single(n.Rows.Items);
        Assert.Single(n.Platform.Requests);
    }
}
