using Diten.PvgService.Api;

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var environmentName =
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
    Environments.Production;

var urls =
    Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ??
    "http://127.0.0.1:5011";

new WebHostBuilder()
    .UseKestrel()
    .UseContentRoot(AppContext.BaseDirectory)
    .UseConfiguration(configuration)
    .UseEnvironment(environmentName)
    .UseUrls(urls)
    .ConfigureServices((context, services) =>
    {
        services.AddRouting();
        services.AddPvgServiceApiHost(context.Configuration, context.HostingEnvironment);
    })
    .Configure(app =>
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPvgServiceHealthEndpoints();
            endpoints.MapPvgCaseIntakeTriageEndpoints();
        });
    })
    .Build()
    .Run();

public partial class Program;
