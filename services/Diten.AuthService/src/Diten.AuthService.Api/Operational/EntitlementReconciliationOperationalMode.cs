using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Diten.AuthService.Persistence;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Api.Operational;

public static class EntitlementReconciliationOperationalMode
{
    public static bool IsRequested(string[] args) => args.Any(a =>
        a.StartsWith(EntitlementReconciliationCommandOptions.Selector, StringComparison.OrdinalIgnoreCase));

    public static async Task<int> RunAsync(string[] args, CancellationToken ct = default)
    {
        try
        {
            // Parse and process-environment gate precede configuration, DI and any network access.
            var options = EntitlementReconciliationCommandOptions.Parse(args);
            if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT", EnvironmentVariableTarget.Process) != "Development"
                || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", EnvironmentVariableTarget.Process) != "Development")
                throw new InvalidOperationException("DEVELOPMENT_PROCESS_PROVENANCE_REQUIRED");
            var token = Environment.GetEnvironmentVariable("DITEN_ENTITLEMENT_OPERATOR_ACCESS_TOKEN", EnvironmentVariableTarget.Process);
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("OPERATOR_TOKEN_REQUIRED");
            var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false).AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables().Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
            services.Configure<PlatformServiceOptions>(configuration.GetSection(PlatformServiceOptions.SectionName));
            services.AddSingleton<ISecretRotationResolver>(new JwtSecretRotationResolver(configuration));
            services.AddSingleton<ITokenService, TokenService>();
            services.AddHttpClient<ITenantEntitlementClient, PlatformTenantEntitlementClient>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<PlatformServiceOptions>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 30));
            });
            services.AddEntitlementReconciliationPersistence(configuration);
            services.AddSingleton<EntitlementReconciliationOperationalRunner>();
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
            var result = await provider.GetRequiredService<EntitlementReconciliationOperationalRunner>().RunAsync(options, token, ct);
            Console.WriteLine(result);
            return result.StartsWith("SUCCESS", StringComparison.Ordinal) || result.StartsWith("PLAN", StringComparison.Ordinal) ? 0 : 2;
        }
        catch (Exception exception)
        {
            // Never emit exceptions containing JWTs, connection strings, credentials or HTTP bodies — only a fixed code.
            Console.Error.WriteLine(FailureLine(exception));
            return 2;
        }
    }

    public const string FailedClosed = "ENTITLEMENT_OPERATION_FAILED_CLOSED";

    /// <summary>
    /// The stderr line (BL570 FIX2 item 2): the same first token as before, plus <c>reason=CODE</c> when the failure is a
    /// named one (OperationalReason). Exit code unchanged (2).
    /// </summary>
    public static string FailureLine(Exception exception) =>
        OperationalReason.From(exception) is { } reason ? FailedClosed + " reason=" + reason : FailedClosed;
}
