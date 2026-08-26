using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.BackgroundJobs;

/// <summary>
/// BL-065 — the sweep that sends due-soon reminders. Deliberately the SAME shape as
/// <see cref="TaskRecurrenceSweepJob"/>: a recurring job runs with no tenant context, so it walks every active
/// tenant and executes the tenant-scoped command inside <c>TenantScope.Begin</c>. Without that scope a read would
/// see another tenant's tasks and email their people — a leak of work AND of information.
///
/// <para><b>Idempotency does not live here.</b> It lives in the command's per-deadline claim on the task, for the
/// same reason the recurrence sweep leaves it to its rule claim: a guard that only exists in the scheduler
/// protects nothing when the command is triggered by hand.</para>
///
/// <para><b>What actually keeps it off.</b> Two switches guard it — <c>BackgroundJobs:RegisterStandardJobs</c>
/// and <c>EnabledJobs["Diten.Platform.MOD-0024.TaskDueSoonSweepJob"]</c> — but the first is TRUE in both appsettings.json and
/// appsettings.Development.json, so in a running Development environment the per-job flag is the ONLY thing
/// holding it. Production adds one more real gate: <c>BackgroundJobs:Enabled</c> is false there, which stops the
/// whole scheduler. "The reminder never arrived" is therefore a configuration question first — check the per-job
/// flag before filing a defect (BL-055).</para>
/// </summary>
public sealed class TaskDueSoonSweepJob : IBackgroundJobHandler<TaskDueSoonSweepJobArgs>
{
    private const int DefaultMaxTasksPerTenant = 200;

    private readonly ITenantRegistryRepository _tenantRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<TaskDueSoonSweepJob> _logger;

    public TaskDueSoonSweepJob(
        ITenantRegistryRepository tenantRegistry,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<TaskDueSoonSweepJob> logger)
    {
        _tenantRegistry = tenantRegistry;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(
        TaskDueSoonSweepJobArgs args,
        BackgroundJobContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var maxTasks = args.MaxTasksPerTenant <= 0 ? DefaultMaxTasksPerTenant : args.MaxTasksPerTenant;
        var correlationId = context.EffectiveCorrelationId.ToString();

        var tenants = await _tenantRegistry.GetActiveTenantsAsync(cancellationToken);
        var sent = 0;
        var alreadyReminded = 0;
        // Per-TASK failures, distinct from per-TENANT ones. The command already counts them and the first
        // version of this job threw the number away, so a tenant whose every send blew up was logged as a clean
        // run — a job reporting success it did not have, which is the defect this whole area keeps producing.
        var notDelivered = 0;
        var failedTasks = 0;
        var failedTenants = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    var response = await _mediator.Send(
                        new SendDueSoonRemindersCommand(NowUtc: null, MaxTasks: maxTasks, correlationId),
                        cancellationToken);

                    if (response.Data is { } data)
                    {
                        sent += data.RemindersSent;
                        alreadyReminded += data.AlreadyReminded;
                        notDelivered += data.NotDelivered;
                        failedTasks += data.Failed;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedTenants++;
                _logger.LogWarning(
                    ex,
                    "task.duesoon.sweep.tenant_failed TenantId={TenantId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    tenant.Id,
                    ex.GetType().Name,
                    correlationId);
            }
        }

        /*
         * A run that lost work does not get to look like a quiet one. The counters are the same either way; the
         * LEVEL is what a reader scanning a log actually sees, and the first live failure was reported at
         * Information with four zeroes beside it.
         */
        var lostWork = notDelivered + failedTasks + failedTenants;
        const string completedTemplate =
            "task.duesoon.sweep.completed Tenants={Tenants} Sent={Sent} AlreadyReminded={AlreadyReminded} "
            + "NotDelivered={NotDelivered} FailedTasks={FailedTasks} FailedTenants={FailedTenants} "
            + "CorrelationId={CorrelationId}";

        if (lostWork > 0)
        {
            _logger.LogWarning(
                completedTemplate,
                tenants.Count, sent, alreadyReminded, notDelivered, failedTasks, failedTenants, correlationId);
        }
        else
        {
            _logger.LogInformation(
                completedTemplate,
                tenants.Count, sent, alreadyReminded, notDelivered, failedTasks, failedTenants, correlationId);
        }
    }
}
