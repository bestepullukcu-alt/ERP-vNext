using System.Globalization;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Platform.Application.Features.Tasks.Recovery;

/// <summary>
/// BL-540 (1) · WP-WF-ENGINE-RECOVERY-01 FIX1 (CT decision D) — THE ORPHAN RECOVERY IS AN OPERATOR'S ONE-SHOT COMMAND, run on
/// the server against the Platform API host (`--run-workflow-orphan-recovery`): no HTTP endpoint, no permission key. Running
/// it takes access to the server and its environment (SAP: an administrator's background report; Oracle: an ESS job).
/// <list type="bullet">
/// <item><c>--tenant &lt;id&gt;</c> — REQUIRED: one tenant per run, and the run is scoped to it.</item>
/// <item>(no other argument) — DRY RUN: lists and counts the tenant's orphans, closes nothing.</item>
/// <item><c>--apply</c> — closes each orphan's open approval task through the engine's own cancel.</item>
/// <item><c>--expect N</c> — nothing is closed unless exactly N orphans are found.</item>
/// </list>
/// The connection comes from the host's own configuration and environment, never the command line, and is never written to
/// any output: a failure names only the exception's type (the BL-529 FIX9/FIX10 rules).
/// <para>Exit codes: 0 done · 1 failure (type only) · 2 usage · 4 some orphans were not closed (listed) · 5 the count was not
/// the expected one (nothing closed).</para>
/// </summary>
public static class WorkflowOrphanRecoveryCommandLine
{
    public const string Argument = "--run-workflow-orphan-recovery";
    public const string ToolName = "workflow-orphans";

    public static bool IsRequested(IReadOnlyList<string> args) =>
        args.Any(a => a.StartsWith(Argument, StringComparison.OrdinalIgnoreCase));

    /// <param name="services">Builds the application's services; called inside the failure net (a broken configuration or
    /// connection string fails HERE, and only its type is written).</param>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        Func<IServiceProvider> services,
        TextWriter output,
        TextWriter error,
        string machineName,
        CancellationToken ct = default)
    {
        if (!TryParse(args, out var tenant, out var apply, out var expect))
        {
            error.WriteLine($"usage: Diten.Platform.API {Argument} --tenant <tenant-id> [--apply] [--expect N]");
            return 2;
        }

        try
        {
            var provider = services();
            await using var scope = provider.CreateAsyncScope();
            using var tenantScope = TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), tenant);
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var correlationId = Guid.NewGuid().ToString();

            var found = await ScanAsync(mediator, apply: false, correlationId, machineName, ct);
            output.WriteLine($"Tenant: {tenant:D}");
            output.WriteLine($"Orphans (open approvals or reviews nobody owns): {found.Count}");
            foreach (var item in found)
            {
                output.WriteLine($"  instance {item.WorkflowInstanceId:D}  {item.ObjectType} {item.ObjectId}  {item.Reason}  {item.AgeMinutes} min");
            }

            if (expect is { } expected && expected != found.Count)
            {
                error.WriteLine($"Expected {expected} orphan(s), found {found.Count}: nothing was closed.");
                return 5;
            }

            if (!apply)
            {
                output.WriteLine("DRY RUN — nothing closed. Run again with --apply (and --expect N) to close them.");
                return 0;
            }

            var applied = await ScanAsync(mediator, apply: true, correlationId, machineName, ct);
            var left = applied.Where(i => i.Outcome != "CLOSED").ToList();
            if (left.Count > 0)
            {
                error.WriteLine($"WARNING: {left.Count} orphan(s) were not closed:");
                foreach (var item in left)
                {
                    error.WriteLine($"  instance {item.WorkflowInstanceId:D}  ({item.Outcome})");
                }
            }

            output.WriteLine($"APPLIED — {applied.Count - left.Count} of {applied.Count} orphan(s) closed.");
            return left.Count > 0 ? 4 : 0;
        }
        catch (Exception failure)
        {
            // Never the message: a configuration or driver exception can carry the connection string.
            error.WriteLine($"failure ({failure.GetType().Name}); nothing was closed after it, and no message is printed.");
            return 1;
        }
    }

    private static async Task<List<WorkflowOrphanItem>> ScanAsync(
        IMediator mediator, bool apply, string correlationId, string machineName, CancellationToken ct)
    {
        var items = new List<WorkflowOrphanItem>();
        Guid? after = null;
        while (true)
        {
            var page = await mediator.Send(new SweepWorkflowOrphansCommand(
                apply, SweepWorkflowOrphansHandler.MaxMaxItems, correlationId, after, ToolName, machineName), ct);
            if (!page.IsSuccessful || page.Data is null)
            {
                throw new InvalidOperationException(page.ReasonCode ?? "WORKFLOW_ORPHAN_SWEEP_FAILED");
            }

            items.AddRange(page.Data.Items);
            if (!page.Data.More)
            {
                return items;
            }

            after = page.Data.NextAfter;
        }
    }

    private static bool TryParse(IReadOnlyList<string> args, out Guid tenant, out bool apply, out int? expect)
    {
        tenant = Guid.Empty;
        apply = false;
        expect = null;
        var sawCommand = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case Argument when !sawCommand:
                    sawCommand = true;
                    break;
                case "--apply" when !apply:
                    apply = true;
                    break;
                case "--tenant" when tenant == Guid.Empty && i + 1 < args.Count && Guid.TryParse(args[i + 1], out var id) && id != Guid.Empty:
                    tenant = id;
                    i++;
                    break;
                case "--expect" when expect is null && i + 1 < args.Count
                                     && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var n):
                    expect = n;
                    i++;
                    break;
                default:
                    return false;
            }
        }

        return sawCommand && tenant != Guid.Empty;
    }
}
