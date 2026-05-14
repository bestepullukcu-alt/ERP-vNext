using Diten.DevEnablementService.Application;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.DevEnablementService.Infrastructure;
using Diten.DevEnablementService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Katman DI ──────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddSecretsProvider(builder.Configuration, builder.Environment, options => options.ServiceName = "DevEnablement");
builder.Services.ValidateRequiredSecrets(builder.Configuration, builder.Environment, "DevEnablement", [
    new("JwtSettings:Secret", "DevEnablement", SecretRequirementKind.JwtCurrent),
    new("JwtSettings:PreviousSecrets", "DevEnablement", SecretRequirementKind.JwtPreviousCollection, Required: false),
    new("Mongo:ConnectionString", "DevEnablement", SecretRequirementKind.ConnectionString)
]);
builder.Services.AddInfrastructure();
builder.Services.AddPersistence(builder.Configuration, builder.Environment);

// ── JWT ───────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = builder.Configuration["JwtSettings:Audience"];
var jwtRotationResolver = new JwtSecretRotationResolver(builder.Configuration);

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
            IssuerSigningKeys = jwtRotationResolver.GetValidationKeys(),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder => builder
            .SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});


// ── Controllers + ProblemDetails ──────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// ── Swagger ───────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Diten DevEnablement Service",
        Version = "v1"
    });

    // Swagger'da JWT + X-Tenant-Id desteği
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer token. Örnek: Bearer <token>"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    c.AddSecurityDefinition("X-Tenant-Id", new OpenApiSecurityScheme
    {
        Name = "X-Tenant-Id",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Multi-tenant GUID. Örnek: 3fa85f64-5717-4562-b3fc-2c963f66afa6"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "X-Tenant-Id"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build ─────────────────────────────────────────────────────────────────
var app = builder.Build();

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    // Development: show full exception details for faster debugging (e.g., Mongo connectivity / serialization issues)
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();   // global ProblemDetails
}

app.UseStatusCodePages();

app.UseCors("AllowAllOrigins");

app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();
app.UseModuleSeedBootstrap();


app.MapControllers();

app.Run();
