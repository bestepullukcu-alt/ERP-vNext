using Hangfire.Dashboard;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Diten.Platform.Infrastructure.BackgroundJobs;

public sealed class PlatformActorHangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (PlatformActorDashboardAuthorization.IsAuthorized(httpContext.User))
        {
            return true;
        }

        var authorization = httpContext.Request.Headers.Authorization.ToString();

        var authenticateResult = httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)
            .GetAwaiter()
            .GetResult();

        if (authenticateResult.Succeeded && authenticateResult.Principal is not null)
        {
            httpContext.User = authenticateResult.Principal;
            var isAuthorized = PlatformActorDashboardAuthorization.IsAuthorized(authenticateResult.Principal);
            LogAuthorizationResult(httpContext, isAuthorized, "AuthenticateAsync");
            return isAuthorized;
        }

        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            if (PlatformActorDashboardAuthorization.IsDevelopmentAnonymousBypassAllowed(httpContext))
            {
                LogAuthorizationResult(httpContext, true, "DevelopmentAnonymousLoopbackBypass");
                return true;
            }

            LogAuthorizationResult(httpContext, false, "MissingBearerHeader");
            return false;
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            LogAuthorizationResult(httpContext, false, "EmptyBearerToken");
            return false;
        }

        var configuration = httpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
        if (configuration is null)
        {
            LogAuthorizationResult(httpContext, false, "MissingConfiguration");
            return false;
        }

        var issuer = configuration["JwtSettings:Issuer"];
        var audience = configuration["JwtSettings:Audience"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            LogAuthorizationResult(httpContext, false, "MissingJwtSettings");
            return false;
        }

        try
        {
            var rotationResolver = new JwtSecretRotationResolver(configuration);
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKeys = rotationResolver.GetValidationKeys(),
                    ClockSkew = JwtValidationDefaults.ClockSkew
                },
                out _);

            httpContext.User = principal;
            var isAuthorized = PlatformActorDashboardAuthorization.IsAuthorized(principal);
            LogAuthorizationResult(httpContext, isAuthorized, "ManualJwtValidation");
            return isAuthorized;
        }
        catch (SecurityTokenException ex)
        {
            LogAuthorizationResult(httpContext, false, $"ManualJwtValidationFailed:{ex.GetType().Name}");
            return false;
        }
    }

    private static void LogAuthorizationResult(Microsoft.AspNetCore.Http.HttpContext httpContext, bool authorized, string source)
    {
        var logger = httpContext.RequestServices.GetService(typeof(ILogger<PlatformActorHangfireAuthorizationFilter>))
            as ILogger<PlatformActorHangfireAuthorizationFilter>;

        if (logger is null)
        {
            return;
        }

        var actorType = httpContext.User.Claims
            .FirstOrDefault(claim => string.Equals(claim.Type, "actor_type", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        logger.LogInformation(
            "Hangfire dashboard authorization result. Authorized={Authorized} Source={Source} IsAuthenticated={IsAuthenticated} ActorType={ActorType}",
            authorized,
            source,
            httpContext.User.Identity?.IsAuthenticated == true,
            actorType ?? "(none)");
    }
}
