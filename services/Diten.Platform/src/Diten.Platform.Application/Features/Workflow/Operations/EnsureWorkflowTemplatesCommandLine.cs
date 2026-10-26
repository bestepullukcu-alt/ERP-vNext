using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Platform.Application.Features.Workflow.Operations;

/// <summary>
/// WP-PLT-WORKFLOW-TEMPLATES-01 (owner decision 2026-10-15) — THE OPERATOR'S ONE-SHOT COMMAND that installs the catalog's
/// approval definitions (<see cref="WorkflowTemplateCatalog"/>) in the tenants it is told, run on the server against the
/// Platform API host (<c>--ensure-workflow-templates</c>): no HTTP endpoint, no permission key, never at start-up.
/// <list type="bullet">
/// <item><c>--tenant &lt;id&gt;</c> — REQUIRED, repeatable. Every tenant is checked BEFORE anything is written: the platform
/// system tenant, an unknown tenant and a tenant that is not Active are refused, and then no tenant is written.</item>
/// <item>(no other argument) — DRY RUN: lists what it would do, writes nothing.</item>
/// <item><c>--apply</c> — writes, through the definition's own commands only (Create / Publish, their validation and
/// version rules), as the System actor.</item>
/// </list>
/// Per template: missing → created and published · Draft/Reviewed with no version → published · published and equal to the
/// catalog → left alone · published and different → left alone, "drift" · anything else → left alone, "needs-owner". A
/// tenant's own publication is never overwritten: the publish is sent with the template version read when the decision was
/// made, and a lost race is read again and decided again.
/// <para>Exit codes: 0 done (written or already published; a dry run listed what it would write) · 1 setup or unexpected
/// failure (nothing printed but the exception's type) · 2 drift or needs-owner (nothing was overwritten) · 3 argument or
/// tenant refused (nothing was written) · 4 stopped part-way (tenant and remaining templates named; running again is safe).
/// With several, the first of 1 &gt; 4 &gt; 3 &gt; 2 &gt; 0. (Unlike the orphan recovery, where 2 is usage: here usage is 3.)</para>
/// </summary>
public static class EnsureWorkflowTemplatesCommandLine
{
    public const string Argument = "--ensure-workflow-templates";
    public const string Usage = "usage: Diten.Platform.API " + Argument + " --tenant <tenant-id> [--tenant <tenant-id> ...] [--apply]";

    public const int Done = 0;
    public const int Failure = 1;
    public const int NeedsAttention = 2;
    public const int Refused = 3;
    public const int PartWay = 4;

    public const string CreatedAndPublished = "created+published";
    public const string Published = "published";
    public const string AlreadyPublished = "already-published";
    public const string Drift = "drift";
    public const string NeedsOwner = "needs-owner";
    public const string WouldCreateAndPublish = "would-create+publish";
    public const string WouldPublish = "would-publish";
    public const string OwnLeftoverSuffix = " (own-leftover)";

    private const int MaxDecisionsPerTemplate = 4;

    /// <summary>Only the exact argument (ordinal) selects the mode; a near miss is not this command.</summary>
    public static bool IsRequested(IReadOnlyList<string> args) =>
        args.Any(a => string.Equals(a, Argument, StringComparison.Ordinal));

    /// <param name="services">Builds the application's services; called inside the failure net.</param>
    /// <param name="catalogPath">The catalog file (the host passes the copy next to its own binaries).</param>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        Func<IServiceProvider> services,
        string catalogPath,
        TextWriter output,
        TextWriter error,
        CancellationToken ct = default)
    {
        if (!TryParse(args, out var tenants, out var apply))
        {
            error.WriteLine(Usage);
            error.WriteLine("Nothing was written.");
            return Refused;
        }

        IServiceProvider provider;
        WorkflowTemplateCatalog catalog;
        try
        {
            provider = services();
            catalog = WorkflowTemplateCatalog.Load(catalogPath);
        }
        catch (Exception failure)
        {
            // Never the message: a configuration or driver exception can carry the connection string.
            error.WriteLine($"setup failure ({failure.GetType().Name}); nothing was read or written, and no message is printed.");
            return Failure;
        }

        // Every tenant is checked before ANY write: one refused tenant means nothing is written anywhere.
        List<string> refusals;
        try
        {
            refusals = await RefusedTenantsAsync(provider, tenants, ct);
        }
        catch (Exception failure)
        {
            error.WriteLine($"failure while checking the tenants ({failure.GetType().Name}); nothing was written.");
            return Failure;
        }

        if (refusals.Count > 0)
        {
            foreach (var refusal in refusals)
            {
                error.WriteLine(refusal);
            }

            error.WriteLine("REFUSED — nothing was written to any tenant.");
            return Refused;
        }

        output.WriteLine($"Catalog v{catalog.CatalogVersion}: {catalog.Templates.Count} template(s). Mode: {(apply ? "APPLY" : "DRY RUN")}.");
        var needsAttention = false;
        foreach (var tenant in tenants)
        {
            output.WriteLine($"Tenant {tenant:D}");
            var done = new List<string>();
            try
            {
                await using var scope = provider.CreateAsyncScope();
                using var tenantScope = TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), tenant);
                var ensurer = new TemplateEnsurer(scope.ServiceProvider, catalog, apply);
                foreach (var entry in catalog.Templates)
                {
                    var outcome = await ensurer.EnsureAsync(entry, ct);
                    output.WriteLine($"  {entry.TemplateCode,-28} {outcome}");
                    needsAttention |= outcome is Drift or NeedsOwner;
                    done.Add(entry.TemplateCode);
                }
            }
            catch (Exception failure)
            {
                var remaining = catalog.Templates.Select(t => t.TemplateCode).Except(done, StringComparer.Ordinal).ToList();
                // A refused definition command says which and why (its own status and reason code, never a message from
                // elsewhere); any other failure is named by its type only.
                var cause = failure switch
                {
                    TemplateCommandRefusedException refused =>
                        $"{refused.TemplateCode}: {refused.Command} answered {refused.StatusCode} {refused.ReasonCode ?? "(no reason code)"}",
                    TemplateCommandFailedException failed =>
                        $"{failed.TemplateCode}: {failed.Command} failed ({failed.InnerException?.GetType().Name})",
                    _ => failure.GetType().Name
                };
                error.WriteLine($"STOPPED ({cause}) in tenant {tenant:D}; not completed: {string.Join(", ", remaining)}"
                    + (tenants.Count > 1 ? $"; tenants after it not reached: {string.Join(", ", tenants.SkipWhile(t => t != tenant).Skip(1).Select(t => t.ToString("D")))}" : string.Empty)
                    + ". Running the command again is safe (what exists is decided again, nothing is overwritten).");
                return apply ? PartWay : Failure;
            }
        }

        if (!apply)
        {
            output.WriteLine("DRY RUN — nothing written. Run again with --apply to write the 'would-' rows.");
        }

        if (needsAttention)
        {
            error.WriteLine("NOTICE: some templates are 'drift' or 'needs-owner': they exist in another form and were left as they are.");
            return NeedsAttention;
        }

        return Done;
    }

    private static async Task<List<string>> RefusedTenantsAsync(IServiceProvider provider, IReadOnlyList<Guid> tenants, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistryRepository>();
        var refusals = new List<string>();
        foreach (var tenant in tenants)
        {
            // The platform system tenant has no approval definitions of its own (it is the platform administrators' home).
            if (SystemTenantRules.IsSystemTenantId(tenant))
            {
                refusals.Add($"tenant {tenant:D}: the platform system tenant is refused.");
                continue;
            }

            var record = await registry.GetByIdAsync(tenant, ct);
            if (record is null || record.IsDeleted)
            {
                refusals.Add($"tenant {tenant:D}: no such tenant.");
            }
            else if (record.Status != TenantStatus.Active)
            {
                refusals.Add($"tenant {tenant:D}: status {record.Status}, only an Active tenant is accepted.");
            }
        }

        return refusals;
    }

    private static bool TryParse(IReadOnlyList<string> args, out List<Guid> tenants, out bool apply)
    {
        tenants = [];
        apply = false;
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
                case "--tenant" when i + 1 < args.Count && Guid.TryParse(args[i + 1], out var id) && id != Guid.Empty && !tenants.Contains(id):
                    tenants.Add(id);
                    i++;
                    break;
                default:
                    return false;
            }
        }

        return sawCommand && tenants.Count > 0;
    }

    /// <summary>One tenant's decisions, in the tenant scope the caller opened.</summary>
    private sealed class TemplateEnsurer(IServiceProvider services, WorkflowTemplateCatalog catalog, bool apply)
    {
        private readonly IMediator _mediator = services.GetRequiredService<IMediator>();
        private readonly IWorkflowTemplateRepository _templates = services.GetRequiredService<IWorkflowTemplateRepository>();
        private readonly IWorkflowTemplateVersionRepository _versions = services.GetRequiredService<IWorkflowTemplateVersionRepository>();

        public async Task<string> EnsureAsync(WorkflowTemplateCatalogEntry entry, CancellationToken ct)
        {
            var created = false;
            var published = false;
            var ownLeftover = false;
            for (var decision = 0; decision < MaxDecisionsPerTemplate; decision++)
            {
                // Every decision starts from what is stored NOW (a lost race is read again and decided again).
                var template = await _templates.GetByTemplateCodeAsync(entry.TemplateCode, ct);
                if (template is not null && template.Status == WorkflowTemplateStatus.Published)
                {
                    return await ClassifyPublishedAsync(template, entry, created, published, ownLeftover, ct);
                }

                var readiness = template is null ? Readiness.Missing : await ReadinessOfAsync(template, ct);
                if (readiness == Readiness.Designed)
                {
                    return NeedsOwner;
                }

                ownLeftover |= readiness == Readiness.OwnLeftover;

                // The only gate before a write: without --apply nothing below runs.
                if (!apply)
                {
                    return template is null ? WouldCreateAndPublish : WouldPublish + (ownLeftover ? OwnLeftoverSuffix : string.Empty);
                }

                if (template is null)
                {
                    Response<WorkflowDefinitionDetailDto>? create = null;
                    Exception? createFailure = null;
                    try
                    {
                        create = await _mediator.Send(new CreateWorkflowDefinitionCommand(
                            new CreateWorkflowDefinitionRequest(entry.TemplateCode, entry.Name, entry.Description), CorrelationId()), ct);
                    }
                    catch (Exception failure) when (failure is not OperationCanceledException)
                    {
                        createFailure = failure;
                    }

                    if (create?.IsSuccessful == true)
                    {
                        created = true;
                        continue;
                    }

                    // The ONLY create failure that is decided again: the definition EXISTS now — another writer won the
                    // race (the handler's 409, or the store's duplicate key on the unique code). Anything else stops the
                    // run, named: the command's own status and reason, or the exception's type.
                    if (await _templates.GetByTemplateCodeAsync(entry.TemplateCode, ct) is not null)
                    {
                        continue;
                    }

                    throw createFailure is not null
                        ? new TemplateCommandFailedException(entry.TemplateCode, "create", createFailure)
                        : new TemplateCommandRefusedException(entry.TemplateCode, "create", create!.StatusCode, create.ReasonCode);
                }

                Response<PublishWorkflowDefinitionResponse> publish;
                try
                {
                    publish = await _mediator.Send(new PublishWorkflowDefinitionCommand(
                    template.Id,
                    new PublishWorkflowDefinitionRequest(
                        DefinitionJson: entry.DefinitionJson,
                        SchemaVersion: entry.SchemaVersion,
                        ExpressionVersion: entry.ExpressionVersion,
                        ExpectedTemplateVersion: template.Version,
                        ExpectedRowVersion: null,
                        PublishReason: catalog.PublishReason),
                    CorrelationId()), ct);
                }
                catch (Exception failure) when (failure is not OperationCanceledException)
                {
                    // A publish exception is never swallowed and never decided again: the version or the pointer may already
                    // be written. The run stops, named (exit 4); the next run reads what is stored and decides again.
                    throw new TemplateCommandFailedException(entry.TemplateCode, "publish", failure);
                }

                published |= publish.IsSuccessful;
                // Only a 409 (a tenant published or changed it meanwhile) is read again and decided again. Any other failure
                // — a 400, or a 500 the pipeline made of an exception, e.g. after the version or the pointer was written —
                // stops the run: it is never reported as published.
                if (!publish.IsSuccessful && publish.StatusCode != 409) throw new TemplateCommandRefusedException(entry.TemplateCode, "publish", publish.StatusCode, publish.ReasonCode);
            }

            throw new InvalidOperationException($"WORKFLOW_TEMPLATE_UNDECIDED:{entry.TemplateCode}");
        }

        private enum Readiness
        {
            Missing,
            /// <summary>Draft / Reviewed with no version: the catalog's to publish.</summary>
            Publishable,
            /// <summary>Draft / Reviewed whose every version is THIS command's own unfinished publish (System actor, the
            /// command's catalog reason): published again — no tenant designed it.</summary>
            OwnLeftover,
            /// <summary>Anything else: someone designed it (needs-owner).</summary>
            Designed
        }

        private async Task<Readiness> ReadinessOfAsync(WorkflowTemplate template, CancellationToken ct)
        {
            if (template.Status is not (WorkflowTemplateStatus.Draft or WorkflowTemplateStatus.Reviewed))
            {
                return Readiness.Designed;
            }

            var versions = await _versions.ListByTemplateIdAsync(template.Id, ct);
            if (versions.Count == 0)
            {
                return Readiness.Publishable;
            }

            return versions.All(IsOwnVersion) ? Readiness.OwnLeftover : Readiness.Designed;
        }

        private static bool IsOwnVersion(WorkflowTemplateVersion version) =>
            string.Equals(version.PublishedBy, SystemActor, StringComparison.Ordinal)
            && version.PublishReason is { } reason
            && reason.StartsWith(WorkflowTemplateCatalog.PublishReasonPrefix, StringComparison.Ordinal);

        private async Task<string> ClassifyPublishedAsync(
            WorkflowTemplate template, WorkflowTemplateCatalogEntry entry, bool created, bool published, bool ownLeftover,
            CancellationToken ct)
        {
            if (template.ActivePublishedVersionId is null)
            {
                return NeedsOwner;
            }

            var active = await _versions.GetActivePublishedVersionAsync(template.Id, ct);
            if (active is null)
            {
                return NeedsOwner;
            }

            if (!entry.Matches(active.DefinitionJson, active.SchemaVersion, active.ExpressionVersion))
            {
                return Drift;
            }

            var outcome = created && published ? CreatedAndPublished : published ? Published : AlreadyPublished;
            return published && ownLeftover ? outcome + OwnLeftoverSuffix : outcome;
        }

        private static string CorrelationId() => $"workflow-templates:{Guid.NewGuid():N}";
    }

    /// <summary>The actor name the definition commands record without a request (CurrentUserContext).</summary>
    private const string SystemActor = "system";

    /// <summary>A definition command threw: the run stops (exit 4), naming the template, the command and the exception's TYPE
    /// (never its message).</summary>
    private sealed class TemplateCommandFailedException(string templateCode, string command, Exception inner)
        : Exception($"WORKFLOW_TEMPLATE_COMMAND_FAILED:{templateCode}:{command}", inner)
    {
        public string TemplateCode { get; } = templateCode;
        public string Command { get; } = command;
    }

    /// <summary>A definition command refused the catalog's write with something other than a race: the run stops (exit 4).</summary>
    private sealed class TemplateCommandRefusedException(string templateCode, string command, int statusCode, string? reasonCode)
        : Exception($"WORKFLOW_TEMPLATE_COMMAND_REFUSED:{templateCode}:{command}:{statusCode}:{reasonCode}")
    {
        public string TemplateCode { get; } = templateCode;
        public string Command { get; } = command;
        public int StatusCode { get; } = statusCode;
        public string? ReasonCode { get; } = reasonCode;
    }
}
