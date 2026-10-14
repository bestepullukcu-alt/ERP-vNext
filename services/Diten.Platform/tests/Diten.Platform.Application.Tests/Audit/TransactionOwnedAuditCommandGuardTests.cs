using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Application.Common;
using Diten.Platform.Common.Authorization;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 — the marker (<see cref="ITransactionOwnedAuditCommand"/>) takes a command OUT of the
/// central audit pipeline, so it must never be a way around auditing. Three things are held here:
/// <list type="number">
/// <item>the allow-list <c>AuditBehavior</c> checks (<see cref="TransactionOwnedAuditCommands.Authorized"/>) is exactly
/// the set of marked commands — neither a second, drifting copy nor a superset (eight marked subscription commands
/// were missing from it for a month and could not run at all);</item>
/// <item>a marked command the list does not name is refused by the pipeline before its handler runs;</item>
/// <item>every marked command's handler actually HOLDS one of the three doors that lead to the in-transaction writer.</item>
/// </list>
/// The architecture ledger measures the same from the source side: the trail's token is the write CALL, not the marker.
/// </summary>
public sealed class TransactionOwnedAuditCommandGuardTests
{
    private static readonly Type[] Marked = typeof(ITransactionOwnedAuditCommand).Assembly.GetTypes()
        .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(ITransactionOwnedAuditCommand).IsAssignableFrom(type))
        .ToArray();

    // The three production types through which a handler reaches the in-transaction audit writer.
    private static readonly Type[] Doors =
    [
        typeof(ITransactionalAuditOutboxWriter),
        typeof(TenantSubscriptionTransactionWriter),
        typeof(IGlobalApplicabilityTransactionCoordinator)
    ];

    // WP-LANE-MAIN-SYNC-02 P1 (owner decision 2026-10-14, BL-634) — the MOD-0022 e-signature commands that came with
    // main: AuditBehavior lets them through ESignatureTransactionAudit, not TransactionOwnedAuditCommands. Named one by one
    // (exact type names), with the owner and the reason; the list only shrinks (a row the code no longer needs is red).
    private const string ESignOwner = "MOD-0022 e-imza geliştiricisi";
    private const string ESignReason = "main-sync 2026-10-14";
    private const string ESignBacklog = "BL-634";
    internal static readonly IReadOnlyDictionary<string, (string Owner, string Reason, string Backlog)> MainSyncESignCommands =
        new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            ["CancelSignatureRequestCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["CreateExportCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["CreateMeaningCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["CreateSampleCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["CreateSignatureRequestCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["DeactivateSampleCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["DecideSampleCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["ExpireSignatureRequestsCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["InvalidateSignatureCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["RegisterSignableActionsCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["RejectSignatureRequestCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["RevertLegalEntityPolicyCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["SavePolicyCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["SetMeaningActiveCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["SignSubjectCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["UpdateMeaningCommand"] = (ESignOwner, ESignReason, ESignBacklog),
            ["WithdrawSampleCommand"] = (ESignOwner, ESignReason, ESignBacklog)
        };

    // …and the one whose handler writes through the central audit service (a platform-global catalog event), not the
    // in-transaction door: named for the door check below, same owner, reason and backlog item.
    internal static readonly IReadOnlyDictionary<string, (string Owner, string Reason, string Backlog)> MainSyncESignWithoutADoor =
        new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            ["RegisterSignableActionsCommand"] = (ESignOwner, ESignReason, ESignBacklog)
        };

    [Fact]
    public void The_pipelines_allow_list_is_exactly_the_marked_commands()
    {
        Assert.True(Marked.Length >= 25, $"Only {Marked.Length} marked commands were found — the scan is not reading the assembly.");

        var eSignNamed = Marked.Where(t => MainSyncESignCommands.ContainsKey(t.Name)).ToArray();
        // A named row must still be a marked command AuditBehavior lets through ESignatureTransactionAudit — or it leaves.
        var staleNamed = MainSyncESignCommands.Keys.Except(eSignNamed.Where(Diten.Platform.Application.Features.ESignature.ESignatureTransactionAudit.Owns)
            .Where(t => !TransactionOwnedAuditCommands.Authorized.Contains(t)).Select(t => t.Name)).OrderBy(n => n).ToArray();
        Assert.True(staleNamed.Length == 0, $"Named e-signature rows that no longer need their exception (the list only shrinks): {string.Join(", ", staleNamed)}");
        Assert.All(MainSyncESignCommands, row => Assert.Equal((ESignOwner, ESignReason, ESignBacklog), row.Value));

        var markedButNotListed = Marked.Except(TransactionOwnedAuditCommands.Authorized).Except(eSignNamed)
            .Select(t => t.Name).OrderBy(n => n).ToArray();
        var listedButNotMarked = TransactionOwnedAuditCommands.Authorized.Except(Marked).Select(t => t.Name).OrderBy(n => n).ToArray();

        Assert.True(markedButNotListed.Length == 0,
            $"Marked but not on AuditBehavior's allow-list — these commands cannot run at all: {string.Join(", ", markedButNotListed)}");
        Assert.True(listedButNotMarked.Length == 0,
            $"On the allow-list without the marker: {string.Join(", ", listedButNotMarked)}");
    }

    [Fact]
    public async Task A_marked_command_the_list_does_not_name_is_refused_before_its_handler_runs()
    {
        var behavior = new AuditBehavior<UnlistedMarkedCommand, Response<NoContent>>(
            Mock.Of<IAuditService>(MockBehavior.Strict), Mock.Of<ITenantAuthorizationContext>(), new AuditBehaviorOptions(),
            NullLogger<AuditBehavior<UnlistedMarkedCommand, Response<NoContent>>>.Instance);
        var handlerRan = false;

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new UnlistedMarkedCommand(),
            () => { handlerRan = true; return Task.FromResult(Response<NoContent>.Success(204)); },
            CancellationToken.None));

        Assert.Contains("Transaction-owned audit is not authorized", refusal.Message);
        Assert.False(handlerRan);
    }

    [Fact]
    public void Every_marked_commands_handler_holds_a_door_to_the_in_transaction_writer()
    {
        var application = typeof(ITransactionOwnedAuditCommand).Assembly;
        var withoutADoor = new List<string>();

        foreach (var command in Marked)
        {
            var handler = application.GetTypes().SingleOrDefault(type => type is { IsAbstract: false, IsInterface: false }
                && type.GetInterfaces().Any(i => i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                    && i.GetGenericArguments()[0] == command));
            if (handler is null)
            {
                withoutADoor.Add($"{command.Name} (no handler)");
                continue;
            }

            var dependencies = handler.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).ToArray();
            if (!dependencies.Any(dependency => Doors.Contains(dependency)) && !MainSyncESignWithoutADoor.ContainsKey(command.Name))
            {
                withoutADoor.Add($"{command.Name} ({handler.Name})");
            }
        }

        // A named row whose handler has since found a door leaves the list (the list only shrinks).
        var staleDoorRows = MainSyncESignWithoutADoor.Keys.Where(name => Marked.All(t => t.Name != name)
            || application.GetTypes().Any(type => type.GetInterfaces().Any(i => i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                && i.GetGenericArguments()[0].Name == name)
                && type.GetConstructors().SelectMany(c => c.GetParameters()).Any(p => Doors.Contains(p.ParameterType)))).ToArray();
        Assert.True(staleDoorRows.Length == 0, $"Named rows whose handler now holds a door (remove them): {string.Join(", ", staleDoorRows)}");

        Assert.True(withoutADoor.Count == 0,
            "Marked ITransactionOwnedAuditCommand, so the central pipeline does not audit it — but its handler cannot reach the "
            + $"in-transaction writer either, so nothing would: {string.Join(", ", withoutADoor)}");
    }

    /// <summary>
    /// The two commands no person sends name the job that does, and tell Platform's own startup worker from another
    /// service's push; the module code is recorded as what the manifest DECLARES, never as an actor.
    /// </summary>
    [Fact]
    public void The_unattended_callers_name_their_job_and_the_two_manifest_doors_are_told_apart()
    {
        var root = RepoRoot();
        var register = File.ReadAllText(Path.Combine(root, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "ModuleRegistration", "RegisterModuleManifestCommandHandler.cs"));
        var seed = File.ReadAllText(Path.Combine(root, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "SubscriptionPlans", "Handlers", "CommandHandlers", "SeedDefaultSubscriptionPlansCommandHandler.cs"));
        var endpoint = File.ReadAllText(Path.Combine(root, "services", "Diten.Platform", "src", "Diten.Platform.API", "Controllers", "Internal", "InternalModuleRegistrationController.cs"));
        var worker = File.ReadAllText(Path.Combine(root, "services", "Diten.Platform", "src", "Diten.Platform.API", "Services", "ModuleRegistration", "PlatformModuleSelfRegistrationWorker.cs"));

        Assert.Contains("SystemActor: pushedOverInternalEndpoint ? ModuleManifestPushActor : ModuleSelfRegistrationActor", register, StringComparison.Ordinal);
        Assert.Contains("[\"DeclaredModuleCode\"] = moduleCode", register, StringComparison.Ordinal);
        Assert.NotEqual(
            Diten.Platform.Application.Features.ModuleRegistration.RegisterModuleManifestCommandHandler.ModuleManifestPushActor,
            Diten.Platform.Application.Features.ModuleRegistration.RegisterModuleManifestCommandHandler.ModuleSelfRegistrationActor);
        Assert.Contains("SystemActor: \"subscription-plan-startup-seed\"", seed, StringComparison.Ordinal);
        Assert.Contains("PushedOverInternalEndpoint: true", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("PushedOverInternalEndpoint", worker, StringComparison.Ordinal); // the in-process worker is the default
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.Platform", "src", "Diten.Platform.Application")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }

    private sealed record UnlistedMarkedCommand : IRequest<Response<NoContent>>, ITransactionOwnedAuditCommand;
}
