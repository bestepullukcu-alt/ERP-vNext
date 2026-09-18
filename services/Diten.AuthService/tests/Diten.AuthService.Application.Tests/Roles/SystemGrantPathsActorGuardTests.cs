using System.Text.RegularExpressions;
using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;
using Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Infrastructure.Eventing;
using Diten.AuthService.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// BL-412 — the person paths (POST/PUT/DELETE api/roles, POST api/roles/{id}/permissions, POST api/users/{id}/roles) stamp
/// the acting user. The system paths must NOT: seed, default role provisioning, entitlement sync, the self-service
/// reconciler, the full-catalog grant, tenant-admin invitation, platform-admin provisioning and anonymous
/// self-registration's default-role row keep their non-person actor. This guard reads the production types by
/// reflection and the production source (comments stripped):
/// (1) no system grant path can see a request principal (ICurrentUserAccessor / IHttpContextAccessor) or dispatch a
/// MediatR command, so none of them can flow through the person handlers with a user context; (2) the person handlers
/// take the actor from the SAME accessor the RBAC audit recorder uses (positive control — proves the detector is not
/// vacuous); (3) the constant each system path writes is still the non-person value; (4) AssignRoleCommand is sent
/// only by the authenticated users endpoint, and every other place that writes a UserRole directly is a classified
/// system writer that keeps the system actor.
/// </summary>
public sealed class SystemGrantPathsActorGuardTests
{
    private static readonly Type[] PersonContextTypes =
        [typeof(ICurrentUserAccessor), typeof(IHttpContextAccessor), typeof(IMediator), typeof(ISender)];

    private const string UsersControllerPath = "Diten.AuthService.Api/Controllers/UsersController.cs";
    private const string AssignRoleHandlerPath = "Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs";

    // BL-412 (CT benchmark D6, decided 2026-09-15) — anonymous self-registration (POST api/auth/register,
    // api/tenant-auth/register) writes the default Viewer row as "system": tenant policy chose the role, not the
    // registrant. The registrant is named where the act is attributable — the tenant_user_self_registered audit event
    // (RegisterCommandHandlerTests, SelfRegistrationActorEndpointTests).
    private static readonly string[] SystemUserRoleWriters =
    [
        "Diten.AuthService.Api/Controllers/InternalEventsController.cs",
        "Diten.AuthService.Api/Controllers/PlatformAuthController.cs",
        "Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RegisterCommandHandler.cs",
        "Diten.AuthService.Persistence/Seed/DataSeeder.cs",
    ];

    private static readonly string[] SystemActorArguments = ["\"system\"", "SystemUser", "SystemActor"];

    private static readonly Regex AssignRoleDispatch = new(@"\bnew\s+AssignRoleCommand\s*\(", RegexOptions.Compiled);
    private static readonly Regex UserRoleConstruction = new(@"\bnew\s+UserRole\s*\(", RegexOptions.Compiled);
    private static readonly Regex FlatUserRoleConstruction = new(@"\bnew\s+UserRole\s*\((?<args>[^()]*)\)", RegexOptions.Compiled);

    public static TheoryData<Type> SystemGrantPaths => new()
    {
        typeof(RoleProvisioningService),
        typeof(FullCatalogPermissionGrantService),
        typeof(EntitlementPermissionSyncService),
        typeof(EntitlementSyncConsumer),
        typeof(InternalEventsController),
        typeof(InternalPermissionsController),
        typeof(DataSeeder),
        typeof(TenantAdminSelfServiceReconciler),
        typeof(RegisterCommandHandler),
    };

    [Theory]
    [MemberData(nameof(SystemGrantPaths))]
    public void System_grant_path_cannot_see_a_person_or_dispatch_the_person_handlers(Type systemPath)
    {
        var offending = DependencyTypes(systemPath)
            .Where(dependency => PersonContextTypes.Any(person => person.IsAssignableFrom(dependency)))
            .Select(dependency => dependency.FullName)
            .ToArray();

        Assert.Empty(offending);
    }

    [Theory]
    [InlineData(typeof(AssignPermissionCommandHandler))]
    [InlineData(typeof(CreateRoleCommandHandler))]
    [InlineData(typeof(UpdateRoleCommandHandler))]
    [InlineData(typeof(DeleteRoleCommandHandler))]
    [InlineData(typeof(AssignRoleCommandHandler))]
    public void Person_handlers_take_the_actor_from_the_accessor_the_audit_recorder_uses(Type personHandler)
    {
        Assert.Contains(typeof(ICurrentUserAccessor), DependencyTypes(personHandler));
        Assert.Contains(typeof(ICurrentUserAccessor), DependencyTypes(typeof(RbacAuditRecorder)));
    }

    [Theory]
    [InlineData(typeof(RoleProvisioningService), "SystemActor", "system")]
    [InlineData(typeof(FullCatalogPermissionGrantService), "GrantActor", "system")]
    [InlineData(typeof(DataSeeder), "SystemUser", "system")]
    [InlineData(typeof(EntitlementSyncConsumer), "Actor", "entitlement-sync")]
    [InlineData(typeof(InternalEventsController), "EntitlementSyncActor", "tenant-provisioning")]
    [InlineData(typeof(RegisterCommandHandler), "SystemActor", "system")]
    public void System_grant_paths_keep_their_non_person_actor(Type systemPath, string constantName, string expected)
    {
        var field = systemPath.GetField(constantName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{systemPath.Name}.{constantName} no longer exists — re-measure the actor it writes.");

        Assert.True(field.IsLiteral, $"{systemPath.Name}.{constantName} must stay a compile-time constant.");
        Assert.Equal(expected, field.GetRawConstantValue());
    }

    // BL-412 F1 — measured caller inventory: the handler's 401-without-actor is only safe while no system flow sends
    // AssignRoleCommand. A new dispatcher must be classified here first.
    [Fact]
    public void AssignRoleCommand_is_sent_only_by_the_authenticated_users_endpoint()
    {
        var dispatchers = SourceFiles()
            .Where(file => AssignRoleDispatch.IsMatch(WithoutComments(File.ReadAllText(file.Full))))
            .Select(file => file.Relative)
            .ToArray();

        Assert.Equal(new[] { UsersControllerPath }, dispatchers);
    }

    [Fact]
    public void Direct_user_role_writers_are_classified_and_the_system_ones_keep_the_system_actor()
    {
        var writers = SourceFiles()
            .Where(file => UserRoleConstruction.IsMatch(WithoutComments(File.ReadAllText(file.Full))))
            .Select(file => file.Relative)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var classified = SystemUserRoleWriters
            .Append(AssignRoleHandlerPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(classified, writers);

        foreach (var path in SystemUserRoleWriters)
        {
            var body = WithoutComments(File.ReadAllText(Path.Combine(SrcRoot(), path)));
            var flat = FlatUserRoleConstruction.Matches(body);

            Assert.DoesNotContain(nameof(ICurrentUserAccessor), body);
            Assert.Equal(UserRoleConstruction.Matches(body).Count, flat.Count);

            foreach (Match construction in flat)
            {
                var actor = construction.Groups["args"].Value.Split(',').Last().Trim();
                Assert.Contains(actor, SystemActorArguments);
                if (actor == SystemLiteral)
                {
                    continue;
                }

                // BL-412 review (CT 2026-09-15) — a NAME is not a value. An identifier argument must be the file's one
                // declaration of that name, a const pinned to the literal "system", so a local, a parameter or a second
                // field of the same name cannot carry a person's id into a system writer's row.
                var name = Regex.Escape(actor);
                var typedDeclarations = Regex.Matches(body, $@"\b(?:var|string|String)\s+{name}\b").Count;
                var assignments = Regex.Matches(body, $@"\b{name}\s*=(?![=>])").Count;
                var pinnedConstants = Regex.Matches(body, $@"\bconst\s+string\s+{name}\s*=\s*""system""\s*;").Count;

                Assert.True(
                    typedDeclarations == 1 && assignments == 1 && pinnedConstants == 1,
                    $"{path}: {actor} must be declared exactly once, as const string {actor} = \"system\"; found "
                    + $"{typedDeclarations} typed declaration(s), {assignments} assignment(s), {pinnedConstants} pinned const(s).");
            }
        }
    }

    private const string SystemLiteral = "\"system\"";

    private static Type[] DependencyTypes(Type type)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                      | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var constructorParameters = type.GetConstructors(declared).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);
        var fields = type.GetFields(declared).Select(f => f.FieldType);
        var methodParameters = type.GetMethods(declared).SelectMany(m => m.GetParameters()).Select(p => p.ParameterType);

        return constructorParameters.Concat(fields).Concat(methodParameters).Distinct().ToArray();
    }

    // Same production-source reading as AccountKindCreationPathsGuardTests.
    private static IEnumerable<(string Full, string Relative)> SourceFiles()
    {
        var root = SrcRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => (f, Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/')));
    }

    private static string SrcRoot()
    {
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location);
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, "services", "Diten.AuthService", "src");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var sibling = Path.Combine(directory, "src", "Diten.AuthService.Persistence", "Seed");
            if (Directory.Exists(sibling))
            {
                return Path.Combine(directory, "src");
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("services/Diten.AuthService/src was not found above the test assembly.");
    }

    private static string WithoutComments(string source)
    {
        var noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(noBlock, @"//[^\r\n]*", string.Empty);
    }
}
