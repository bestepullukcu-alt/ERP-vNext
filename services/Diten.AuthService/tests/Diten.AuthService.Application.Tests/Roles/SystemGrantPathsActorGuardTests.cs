using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Infrastructure.Eventing;
using Diten.AuthService.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// BL-412 — the person paths (POST/PUT api/roles, POST api/roles/{id}/permissions) now stamp the acting user. The
/// system paths must NOT: seed, default role provisioning, entitlement sync, the self-service reconciler and the
/// full-catalog grant keep their non-person actor. This guard reads the production types by reflection:
/// (1) no system path can see a request principal (ICurrentUserAccessor / IHttpContextAccessor) or dispatch a
/// MediatR command, so none of them can flow through the person handlers with a user context; (2) the person
/// handlers take the actor from the SAME accessor the RBAC audit recorder uses (positive control — proves the
/// detector is not vacuous); (3) the constant each system path writes is still the non-person value.
/// </summary>
public sealed class SystemGrantPathsActorGuardTests
{
    private static readonly Type[] PersonContextTypes =
        [typeof(ICurrentUserAccessor), typeof(IHttpContextAccessor), typeof(IMediator), typeof(ISender)];

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
    public void System_grant_paths_keep_their_non_person_actor(Type systemPath, string constantName, string expected)
    {
        var field = systemPath.GetField(constantName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{systemPath.Name}.{constantName} no longer exists — re-measure the actor it writes.");

        Assert.True(field.IsLiteral, $"{systemPath.Name}.{constantName} must stay a compile-time constant.");
        Assert.Equal(expected, field.GetRawConstantValue());
    }

    private static Type[] DependencyTypes(Type type)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                      | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var constructorParameters = type.GetConstructors(declared).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);
        var fields = type.GetFields(declared).Select(f => f.FieldType);
        var methodParameters = type.GetMethods(declared).SelectMany(m => m.GetParameters()).Select(p => p.ParameterType);

        return constructorParameters.Concat(fields).Concat(methodParameters).Distinct().ToArray();
    }
}
