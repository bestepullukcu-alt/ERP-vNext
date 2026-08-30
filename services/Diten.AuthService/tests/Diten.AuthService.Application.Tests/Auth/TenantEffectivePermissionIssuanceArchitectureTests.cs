using System.Reflection;
using System.Runtime.CompilerServices;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

namespace Diten.AuthService.Application.Tests.Auth;

public sealed class TenantEffectivePermissionIssuanceArchitectureTests
{
    [Fact]
    public void Platform_refresh_bypasses_tenant_entitlement_filter()
    {
        var source = ReadApplicationSource("Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs");

        Assert.Contains("var effectivePermissions = isPlatformActor\n            ? permissions\n            : await _effectivePermissionResolver.ResolveAsync", source, StringComparison.Ordinal);
        Assert.Contains("roles,\n                permissions,\n                15,", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_keeps_the_initial_permission_set_empty()
    {
        var source = ReadApplicationSource("Features/Auth/Handlers/CommandHandlers/RegisterCommandHandler.cs");

        Assert.Contains("GenerateAccessToken(created, roles, Array.Empty<string>(), settings.SessionTimeoutMinutes)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ITenantEffectivePermissionResolver", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(LoginCommandHandler))]
    [InlineData(typeof(RefreshTokenCommandHandler))]
    [InlineData(typeof(VerifyMfaCommandHandler))]
    [InlineData(typeof(ForcedChangeTenantPasswordCommandHandler))]
    public void Every_tenant_token_issuance_handler_executes_the_shared_resolver(Type handlerType)
    {
        var methods = handlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(method => method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType)
            .Where(type => type is not null)
            .Select(type => type!.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!)
            .ToArray();

        Assert.NotEmpty(methods);
        Assert.Contains(methods, CallsResolver);
    }

    private static bool CallsResolver(MethodInfo method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        var module = method.Module;
        for (var offset = 0; offset <= il.Length - sizeof(int); offset++)
        {
            var token = BitConverter.ToInt32(il, offset);
            try
            {
                if (module.ResolveMember(token) is MethodInfo candidate
                    && candidate.Name == nameof(ITenantEffectivePermissionResolver.ResolveAsync))
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Most four-byte windows are not metadata tokens.
            }
        }

        return false;
    }

    private static string ReadApplicationSource(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "services/Diten.AuthService/src/Diten.AuthService.Application",
                relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate).Replace("\r\n", "\n", StringComparison.Ordinal);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
