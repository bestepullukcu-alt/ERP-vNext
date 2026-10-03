using Diten.HumanCapitalService.Application;
using Diten.HumanCapitalService.Infrastructure;
using Diten.HumanCapitalService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);

var jwtSecret = builder.Configuration["JwtSettings:Secret"];
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = builder.Configuration["JwtSettings:Audience"];

// Fail at startup: an empty key would otherwise start the host and fail every request with IDX10703.
// A missing issuer or audience would likewise start the host and fail every authenticated request with 401.
ValidateRequiredJwtSetting(jwtSecret, "JwtSettings:Secret", minimumUtf8Bytes: 32);
ValidateRequiredJwtSetting(jwtIssuer, "JwtSettings:Issuer");
ValidateRequiredJwtSetting(jwtAudience, "JwtSettings:Audience");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret ?? string.Empty)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Diten Human Capital Service",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer token."
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Diten.HumanCapitalService" }))
    .WithName("Health")
    .AllowAnonymous();

app.MapControllers();

app.Run();

static void ValidateRequiredJwtSetting(string? value, string key, int minimumUtf8Bytes = 1)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Configuration error: '{key}' is missing or empty.");
    }

    // HMAC-SHA256 signing needs a 256-bit key; same floor as SecretRequirementValidator (JwtCurrent => 32).
    if (Encoding.UTF8.GetByteCount(value) < minimumUtf8Bytes)
    {
        throw new InvalidOperationException($"Configuration error: '{key}' must be at least {minimumUtf8Bytes} bytes.");
    }
}

public partial class Program;
