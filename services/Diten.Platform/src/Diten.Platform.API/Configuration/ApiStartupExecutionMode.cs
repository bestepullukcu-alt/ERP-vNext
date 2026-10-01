using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.API.Services.ModuleRegistration;
using Diten.Platform.API.Services.Security;

namespace Diten.Platform.API.Configuration;

/// <summary>
/// Selects the normal Platform host or the explicitly requested Development API-serving profile.
/// Parsing is deliberately independent of host construction so invalid operational arguments are
/// rejected before configuration-driven database or network work can begin.
/// </summary>
public sealed class ApiStartupExecutionMode
{
    public const string ServeWithoutStartupMaintenanceArgument =
        "--serve-api-without-startup-maintenance";

    private const string InvalidArgumentError = "API_STARTUP_EXECUTION_MODE_ARGUMENT_INVALID";
    private const string ConflictingOperationError = "API_STARTUP_EXECUTION_MODE_OPERATION_CONFLICT";
    private const string EnvironmentNotAllowedError = "API_STARTUP_EXECUTION_MODE_ENVIRONMENT_NOT_ALLOWED";

    private ApiStartupExecutionMode(bool runStartupMaintenance)
    {
        RunStartupMaintenance = runStartupMaintenance;
    }

    public bool RunStartupMaintenance { get; }

    public static ApiStartupExecutionMode Resolve(IReadOnlyList<string> arguments) =>
        Resolve(
            arguments,
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", EnvironmentVariableTarget.Process),
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT", EnvironmentVariableTarget.Process));

    public static ApiStartupExecutionMode Resolve(
        IReadOnlyList<string> arguments,
        string? aspNetCoreEnvironment,
        string? dotNetEnvironment)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var requests = arguments
            .Where(IsServeWithoutMaintenanceLookalike)
            .ToArray();

        if (requests.Length == 0)
        {
            return new ApiStartupExecutionMode(runStartupMaintenance: true);
        }

        if (requests.Length != 1
            || !string.Equals(
                requests[0],
                ServeWithoutStartupMaintenanceArgument,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(InvalidArgumentError);
        }

        if (arguments.Any(argument =>
                !string.Equals(argument, ServeWithoutStartupMaintenanceArgument, StringComparison.Ordinal)
                && argument.StartsWith("--run-", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(ConflictingOperationError);
        }

        var commandLineEnvironments = ReadCommandLineEnvironments(arguments);
        var declaredEnvironments = commandLineEnvironments
            .Append(aspNetCoreEnvironment)
            .Append(dotNetEnvironment)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (declaredEnvironments.Length == 0
            || declaredEnvironments.Any(value =>
                !string.Equals(value, Environments.Development, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(EnvironmentNotAllowedError);
        }

        return new ApiStartupExecutionMode(runStartupMaintenance: false);
    }

    public void ValidateResolvedEnvironment(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        if (!RunStartupMaintenance
            && !string.Equals(environmentName, Environments.Development, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(EnvironmentNotAllowedError);
        }
    }

    public void AddApiStartupMaintenanceServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!RunStartupMaintenance)
        {
            return;
        }

        services.AddHostedService<BusinessReferenceDataCatalogLoadWorker>();
        services.AddHostedService<VerifiedGskuOperationalProvisioningRunner>();
        services.AddSingleton<ModuleSelfRegistrationGate>();
        services.AddHostedService<PlatformModuleSelfRegistrationWorker>();
        services.AddHostedService<PlatformPermissionAutoRegistrationWorker>();
    }

    private static bool IsServeWithoutMaintenanceLookalike(string argument) =>
        argument.StartsWith(ServeWithoutStartupMaintenanceArgument, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string?> ReadCommandLineEnvironments(IReadOnlyList<string> arguments)
    {
        var values = new List<string?>();

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--environment", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith('-'))
                {
                    throw new InvalidOperationException(InvalidArgumentError);
                }

                values.Add(arguments[++index]);
                continue;
            }

            const string environmentPrefix = "--environment=";
            if (argument.StartsWith(environmentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = argument[environmentPrefix.Length..];
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(InvalidArgumentError);
                }

                values.Add(value);
            }
        }

        return values;
    }
}
