using Diten.ApiGateway.Middleware;
using Diten.ApiGateway.Authentication;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.AspNetCore.Authentication;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddSecretsProvider(builder.Configuration, builder.Environment, options => options.ServiceName = "ApiGateway");
builder.Services.ValidateRequiredSecrets(builder.Configuration, builder.Environment, "ApiGateway", [
    new("JwtSettings:Secret", "ApiGateway", SecretRequirementKind.JwtCurrent),
    new("JwtSettings:PreviousSecrets", "ApiGateway", SecretRequirementKind.JwtPreviousCollection, Required: false)
]);

builder.Services.AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, GatewayJwtAuthenticationHandler>("Bearer", _ => { });

builder.Services.AddOcelot();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy
            .WithOrigins("http://localhost:5001", "http://localhost:5011")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseCors("AllowAllOrigins");

app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("Authorization") &&
        context.Request.Cookies.TryGetValue("access_token", out var cookieToken) &&
        !string.IsNullOrWhiteSpace(cookieToken))
    {
        context.Request.Headers.Authorization = $"Bearer {cookieToken}";
    }

    await next();
});

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

await app.UseOcelot();

app.Run();
