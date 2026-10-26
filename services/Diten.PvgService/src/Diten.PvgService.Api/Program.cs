using Diten.PvgService.Api;

CreateWebHostBuilder(args).Build().Run();

public partial class Program
{
    public static IWebHostBuilder CreateWebHostBuilder(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var urls =
            Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ??
            "http://127.0.0.1:5011";

        return PvgServiceApiHost.CreatePvgServiceWebHostBuilder(environmentName, configuration, urls);
    }
}
