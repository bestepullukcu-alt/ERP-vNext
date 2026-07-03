using System.Text;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Authorization;
using Diten.AuthService.Infrastructure.Eventing;
using Diten.AuthService.Infrastructure.Middleware;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSecretsProvider(configuration, environment, options => options.ServiceName = "AuthService");
        services.ValidateRequiredSecrets(configuration, environment, "AuthService", BuildSecretRequirements(configuration));

        // Settings
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.Configure<InternalEventAuthSettings>(configuration.GetSection("InternalEventAuth"));
        services.Configure<PlatformServiceOptions>(configuration.GetSection(PlatformServiceOptions.SectionName));
        services.Configure<MfaOptions>(configuration.GetSection(MfaOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>()
            ?? new JwtSettings();
        var jwtRotationResolver = new JwtSecretRotationResolver(configuration);

        // Authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKeys = jwtRotationResolver.GetValidationKeys(),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

            });

        // Authorization (Permission-based)
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Services
        services.AddScoped<Diten.AuthService.Application.Common.Interfaces.ICurrentUserAccessor, Services.CurrentUserAccessor>(); // FEAT-AUDIT-RBAC
        services.AddScoped<PasswordHasher>(); // concrete class registration
        services.AddScoped<IPasswordHasher>(sp => sp.GetRequiredService<PasswordHasher>());
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddScoped<IInternalEventAuthService, InternalEventAuthService>();
        services.AddScoped<IPlatformAuthEmailService, PlatformAuthEmailService>();
        services.AddScoped<ITenantUserInvitationEmailService, TenantUserInvitationEmailService>();
        services.AddScoped<IMfaChallengeService, MfaChallengeService>();
        services.AddScoped<IOtpDeliveryService, SmtpOtpDeliveryService>();
        services.AddTransient<TenantPropagationHandler>();
        services.AddHttpClient("TenantAwareClient").AddHttpMessageHandler<TenantPropagationHandler>();
        services.AddHttpClient<ITenantLoginSettingsClient, PlatformTenantLoginSettingsClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlatformServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 30));
        });
        services.AddHttpClient<IPlatformAdministratorStatusClient, PlatformAdministratorStatusClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlatformServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 30));
        });
        services.AddHttpClient<ITenantEntitlementClient, PlatformTenantEntitlementClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlatformServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 30));
        });

        // Tenant Context Registration
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        AddEntitlementEventing(services, configuration);

        return services;
    }

    // Cross-service entitlement → role-permission sync transport. Gated by Eventing:Transport
    // (default InMemory ⇒ NOT wired, since in-memory does not cross process boundaries). Setting
    // Transport=RabbitMQ (same broker as Platform) activates the consumer. The MassTransit↔RabbitMQ
    // binding to Platform's exchange is verified end-to-end in S18 (no broker in the unit-test harness).
    private static void AddEntitlementEventing(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthServiceEventingOptions>(configuration.GetSection(AuthServiceEventingOptions.SectionName));

        var options = configuration.GetSection(AuthServiceEventingOptions.SectionName).Get<AuthServiceEventingOptions>()
                      ?? new AuthServiceEventingOptions();

        if (!options.UseRabbitMq)
        {
            return;
        }

        services.AddMassTransit(x =>
        {
            x.AddConsumer<EntitlementSyncConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                    if (options.UseTls)
                    {
                        h.UseSsl(s => s.Protocol = System.Security.Authentication.SslProtocols.Tls12);
                    }
                });

                cfg.UseMessageRetry(r => r.Exponential(
                    options.RetryCount,
                    TimeSpan.FromSeconds(options.InitialRetryDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxRetryDelaySeconds),
                    TimeSpan.FromSeconds(options.InitialRetryDelaySeconds)));
                cfg.ConfigureEndpoints(context);
            });
        });
    }

    private static IReadOnlyList<RequiredSecretDefinition> BuildSecretRequirements(IConfiguration configuration)
    {
        var mfaEnabled = configuration.GetValue<bool>("Mfa:Enabled");
        var smtpEnabled = configuration.GetValue<bool>("Smtp:Enabled");

        return
        [
            new("JwtSettings:Secret", "AuthService", SecretRequirementKind.JwtCurrent),
            new("JwtSettings:PreviousSecrets", "AuthService", SecretRequirementKind.JwtPreviousCollection, Required: false),
            new("MongoDbSettings:ConnectionString", "AuthService", SecretRequirementKind.ConnectionString),
            new("InternalEventAuth:ApiKey", "AuthService", SecretRequirementKind.InternalApiKey),
            new("PlatformService:InternalApiKey", "AuthService", SecretRequirementKind.InternalApiKey),
            new("Mfa:HashSecret", "AuthService", MinimumLength: 32, Required: mfaEnabled, IsEnabled: () => mfaEnabled),
            new("Smtp:Password", "AuthService", MinimumLength: 8, Required: smtpEnabled, IsEnabled: () => smtpEnabled)
        ];
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        return app;
    }
}
