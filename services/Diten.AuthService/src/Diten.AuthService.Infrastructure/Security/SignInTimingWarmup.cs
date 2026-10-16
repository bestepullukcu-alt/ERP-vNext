using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX3 item 7 — makes the sign-in doors' fixed "no account" hash when the host starts,
/// with the registered hasher, so no request pays for it.</summary>
public sealed class SignInTimingWarmup(IServiceScopeFactory scopes) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        SignInTiming.Warm(scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
