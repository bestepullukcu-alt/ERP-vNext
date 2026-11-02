namespace Diten.PvgService.Api.ModuleRegistration;

/// <summary>
/// The two values of the closed catalog registration push (pack "Closed Catalog Registration"). They come from the process
/// environment only: not from <c>IConfiguration</c>, so neither the command line nor a configuration file can supply them.
/// Tests replace this reader in dependency injection and never write to the process environment.
/// </summary>
public interface IPvgModuleRegistrationEnvironment
{
    string? InternalApiKey { get; }

    string? BaseUrl { get; }
}

/// <summary>The default reader: the two process environment variables, read each time they are asked for.</summary>
public sealed class ProcessPvgModuleRegistrationEnvironment : IPvgModuleRegistrationEnvironment
{
    public const string InternalApiKeyVariable = "PlatformRegistration__InternalApiKey";
    public const string BaseUrlVariable = "PlatformRegistration__BaseUrl";

    public string? InternalApiKey => Environment.GetEnvironmentVariable(InternalApiKeyVariable);

    public string? BaseUrl => Environment.GetEnvironmentVariable(BaseUrlVariable);
}
