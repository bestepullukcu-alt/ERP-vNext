namespace Diten.PvgService.Api.ModuleRegistration;

public static class PvgModuleRegistrationServiceCollectionExtensions
{
    public const string HttpClientName = "PvgModuleRegistration";

    // The framework's own request logging writes the request address; it must not reach a log for this call.
    private const string HttpClientLogCategory = "System.Net.Http.HttpClient." + HttpClientName;

    /// <summary>
    /// Registers the closed catalog registration: the provider, the environment reader, the named client (redirects are
    /// not followed, so the internal key never travels to a redirect target), the log filter of that client's category and
    /// the hosted service. Registered on every host; the hosted service decides at start whether to call.
    /// </summary>
    public static IServiceCollection AddPvgModuleRegistration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IModuleManifestProvider, PvgCaseIntakeTriageManifestProvider>();
        services.AddSingleton<IPvgModuleRegistrationEnvironment, ProcessPvgModuleRegistrationEnvironment>();
        services.AddSingleton(PvgModuleRegistrationTiming.Default);
        services
            .AddHttpClient(HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddLogging(builder => builder.AddFilter(HttpClientLogCategory, LogLevel.None));
        services.AddHostedService<PvgModuleRegistrationHostedService>();

        return services;
    }
}
