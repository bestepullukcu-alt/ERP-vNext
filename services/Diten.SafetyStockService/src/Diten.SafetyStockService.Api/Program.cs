using System.Text;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.SafetyStockService.Application;
using Diten.SafetyStockService.Infrastructure;
using Diten.SafetyStockService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var secret = builder.Configuration["JwtSettings:Secret"];
var issuer = builder.Configuration["JwtSettings:Issuer"];
var audience = builder.Configuration["JwtSettings:Audience"];
if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32
    || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
    throw new InvalidOperationException("SafetyStockService requires JwtSettings:Secret (at least 32 bytes), Issuer and Audience.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ClockSkew = JwtValidationDefaults.ClockSkew
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("safety-stock.policies.read", policy => policy.RequireAuthenticatedUser()
        .RequireClaim("permission", "safety-stock.policies.read"));
    options.AddPolicy("safety-stock.evaluations.read", policy => policy.RequireAuthenticatedUser()
        .RequireClaim("permission", "safety-stock.evaluations.read"));
});

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(PolicyReadQueryHandler).Assembly));
builder.Services.AddTransient<MediatR.IPipelineBehavior<PolicyReadQuery, PolicyReadResult>, PolicyReadValidationBehavior>();
builder.Services.AddTransient<MediatR.IPipelineBehavior<PolicyReadQuery, PolicyReadResult>, PolicyReadLoggingBehavior>();
builder.Services.AddTransient<MediatR.IPipelineBehavior<PolicyReadQuery, PolicyReadResult>, PolicyReadExceptionBehavior>();
builder.Services.AddTransient<MediatR.IPipelineBehavior<PolicyReadQuery, PolicyReadResult>, PolicyReadPerformanceBehavior>();
builder.Services.AddScoped<IActorSeriesScopeReader, MdmOnlyLegalEntityScopeReader>();
builder.Services.AddScoped<IPolicyReadStore, UnconfiguredPolicyReadStore>();
builder.Services.AddHttpClient<ILegalEntityReferenceValidator, MdmLegalEntityReferenceValidator>(client =>
{
    var url = builder.Configuration["Mdm:LegalEntitiesUrl"];
    if (Uri.TryCreate(url, UriKind.Absolute, out var address))
        client.BaseAddress = address;
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "Alive", service = "Diten.SafetyStockService" })).AllowAnonymous();
app.MapControllers();
app.Run();

public partial class Program;
