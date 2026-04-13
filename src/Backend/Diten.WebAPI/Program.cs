using Asp.Versioning;
using Diten.Application;
using Diten.Persistence;
using Diten.Infrastructure;
using Diten.WebAPI.Health;
using Diten.WebAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<DeliveryExecutionManagementDependencyHealthCheck>("delivery_execution_management_ppm_dependency");

// Add Layers
builder.Services.AddApplication();
builder.Services.AddPersistence();
// builder.Services.AddInfrastructure(); // Currently empty, but ready

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5002",
                "https://localhost:5002",
                "http://127.0.0.1:5002",
                "https://127.0.0.1:5002",
                "http://localhost:5003",
                "https://localhost:5003",
                "http://127.0.0.1:5003",
                "https://127.0.0.1:5003")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data", "uploads"));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var descriptions = app.DescribeApiVersions();
        foreach (var description in descriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("FrontendPolicy");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/healthz");

var seedTask = DbInitializer.SeedData(app.Services);
var completed = await Task.WhenAny(seedTask, Task.Delay(TimeSpan.FromSeconds(5)));
if (completed == seedTask)
{
    try
    {
        await seedTask;
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database seed skipped during startup.");
    }
}
else
{
    app.Logger.LogWarning("Database seed skipped due to startup timeout.");
}

app.Run();
