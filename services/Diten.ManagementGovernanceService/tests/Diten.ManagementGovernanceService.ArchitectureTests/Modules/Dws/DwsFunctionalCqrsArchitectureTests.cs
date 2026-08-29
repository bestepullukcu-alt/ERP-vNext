using System.Reflection;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using MediatR;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.Dws;

public sealed class DwsFunctionalCqrsArchitectureTests
{
    [Fact]
    public void Query_snapshot_revalidation_seam_has_exact_authoritative_signature()
    {
        var method = typeof(DwsFunctionalQueryStore).GetMethod(
            "RevalidateSnapshotAsync",
            [typeof(DwsRevisionSnapshot), typeof(CancellationToken)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
        Assert.Equal(typeof(DwsFunctionalQueryStore), method.DeclaringType);
    }

    [Fact]
    public void All_five_query_paths_are_bound_to_the_shared_revalidated_snapshot_path()
    {
        var root = FindRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Persistence/Modules/Dws/DwsFunctionalPorts.cs"));

        Assert.Contains("GetStructureByIdAsync(Guid id", source, StringComparison.Ordinal);
        Assert.Contains("GetStructureTreeAsync(Guid id", source, StringComparison.Ordinal);
        Assert.Contains("ValidateStructureAsync(Guid id", source, StringComparison.Ordinal);
        Assert.Contains("CompareStructureRevisionsAsync(Guid id", source, StringComparison.Ordinal);
        Assert.Contains("CompareStructureBaselinesAsync(Guid id", source, StringComparison.Ordinal);
        Assert.True(Count(source, "RevalidatedAsync") >= 6,
            "All five public paths plus the shared helper must remain bound to revalidation.");
        Assert.Contains("foreach (var snapshot in snapshots)", source, StringComparison.Ordinal);
        Assert.Contains("await queries.RevalidateSnapshotAsync(snapshot, cancellationToken)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Functional_surface_has_exact_ten_commands_five_queries_handlers_and_validators()
    {
        var assembly = typeof(DwsTrustedActorContext).Assembly;
        var commands = RequestsIn(assembly, ".Features.Dws.Commands");
        var queries = RequestsIn(assembly, ".Features.Dws.Queries");
        var commandHandlers = HandlersIn(assembly, ".Features.Dws.Handlers.CommandHandlers");
        var queryHandlers = HandlersIn(assembly, ".Features.Dws.Handlers.QueryHandlers");
        var validators = ValidatorsIn(assembly);

        Assert.Equal(10, commands.Length);
        Assert.Equal(5, queries.Length);
        Assert.Equal(10, commandHandlers.Length);
        Assert.Equal(5, queryHandlers.Length);
        Assert.Equal(15, validators.Length);

        Assert.All(commands.Concat(queries), request =>
        {
            var requestContract = request.GetInterfaces().Single(type =>
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>));
            var response = requestContract.GenericTypeArguments.Single();
            Assert.True(response.IsGenericType);
            Assert.Equal(typeof(Response<>), response.GetGenericTypeDefinition());
        });

        Assert.All(validators, validator => Assert.Contains(
            validator.GetInterfaces(),
            contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(IDwsFunctionalValidator<>)));
    }

    [Fact]
    public void All_nine_post_create_commands_and_five_queries_use_the_authoritative_visibility_and_context_fence()
    {
        var root = FindRoot();
        var handlersRoot = Path.Combine(
            root,
            "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/Features/Dws/Handlers");
        var commandFiles = Directory.GetFiles(Path.Combine(handlersRoot, "CommandHandlers"), "*Handler.cs")
            .Where(path => !path.EndsWith("/CreateStructureHandler.cs", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var queryFiles = Directory.GetFiles(Path.Combine(handlersRoot, "QueryHandlers"), "*Handler.cs")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(9, commandFiles.Length);
        Assert.Equal(5, queryFiles.Length);
        foreach (var path in commandFiles.Concat(queryFiles))
        {
            var source = File.ReadAllText(path);
            Assert.Contains("IDwsStructureVisibilityPort visibility", source, StringComparison.Ordinal);
            Assert.Contains("DwsExistingStructureSecurity.CaptureAsync", source, StringComparison.Ordinal);
            Assert.Contains("DwsExistingStructureSecurity.RevalidateAsync", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Global_exception_behavior_remains_byte_exact_HEAD_and_does_not_map_functional_CQRS_responses()
    {
        var root = FindRoot();
        const string relative = "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/Behaviors/ExceptionBehavior.cs";
        var source = File.ReadAllText(Path.Combine(root, relative));
        var head = RunGit(root, "show", $"HEAD:{relative}");

        Assert.Equal(Normalize(head), Normalize(source));
        Assert.Contains("typeof(TResponse) == typeof(Response<DwsLocalResult>)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("typeof(TResponse).GetGenericTypeDefinition()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateStructureResult", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StructureSummaryDto", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DwsFunctionalResponse", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Functional_request_names_are_exact_and_not_generic_dispatch_aliases()
    {
        Assert.Equal(
            new[]
            {
                nameof(AddStructuralDependencyCommand), nameof(AddStructureNodeCommand),
                nameof(CreateNextStructureRevisionCommand), nameof(CreateStructureBaselineCommand),
                nameof(CreateStructureCommand), nameof(MoveStructureNodeCommand),
                nameof(RemoveStructuralDependencyCommand), nameof(RemoveStructureNodeCommand),
                nameof(ReorderStructureNodeCommand), nameof(UpdateStructureMetadataCommand)
            }.Order(StringComparer.Ordinal),
            RequestsIn(typeof(DwsTrustedActorContext).Assembly, ".Features.Dws.Commands")
                .Select(type => type.Name).Order(StringComparer.Ordinal));

        Assert.Equal(
            new[]
            {
                nameof(CompareStructureBaselinesQuery), nameof(CompareStructureRevisionsQuery),
                nameof(GetStructureByIdQuery), nameof(GetStructureTreeQuery), nameof(ValidateStructureQuery)
            }.Order(StringComparer.Ordinal),
            RequestsIn(typeof(DwsTrustedActorContext).Assembly, ".Features.Dws.Queries")
                .Select(type => type.Name).Order(StringComparer.Ordinal));
    }

    private static Type[] TypesIn(Assembly assembly, string namespaceSuffix) => assembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false }
            && type.Namespace?.EndsWith(namespaceSuffix, StringComparison.Ordinal) == true)
        .OrderBy(type => type.Name, StringComparer.Ordinal)
        .ToArray();

    private static Type[] RequestsIn(Assembly assembly, string namespaceSuffix) => TypesIn(assembly, namespaceSuffix)
        .Where(type => type.GetInterfaces().Any(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequest<>)))
        .ToArray();

    private static Type[] HandlersIn(Assembly assembly, string namespaceSuffix) => TypesIn(assembly, namespaceSuffix)
        .Where(type => type.GetInterfaces().Any(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
        .ToArray();

    private static Type[] ValidatorsIn(Assembly assembly) => TypesIn(assembly, ".Features.Dws.Validators")
        .Where(type => type.GetInterfaces().Any(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDwsFunctionalValidator<>)))
        .ToArray();

    private static int Count(string value, string token) =>
        (value.Length - value.Replace(token, string.Empty, StringComparison.Ordinal).Length) / token.Length;

    private static string RunGit(string root, params string[] arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output;
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static string FindRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(Path.Combine(cursor.FullName, "AGENTS.md"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("repo_root_not_found");
    }
}
