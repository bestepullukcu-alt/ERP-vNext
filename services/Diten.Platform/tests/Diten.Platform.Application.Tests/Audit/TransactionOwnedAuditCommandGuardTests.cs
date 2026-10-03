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

    [Fact]
    public void The_pipelines_allow_list_is_exactly_the_marked_commands()
    {
        Assert.True(Marked.Length >= 25, $"Only {Marked.Length} marked commands were found — the scan is not reading the assembly.");

        var markedButNotListed = Marked.Except(TransactionOwnedAuditCommands.Authorized).Select(t => t.Name).OrderBy(n => n).ToArray();
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
            if (!dependencies.Any(dependency => Doors.Contains(dependency)))
            {
                withoutADoor.Add($"{command.Name} ({handler.Name})");
            }
        }

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
