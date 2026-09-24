using Diten.MdmService.Api.ModuleRegistration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Diten.MdmService.Api.Configuration;

/// <summary>
/// Selects the Development-only API-serving path which omits normal startup maintenance.
/// The default remains the existing full startup path.
/// </summary>
public sealed class ApiStartupExecutionMode
{
    public const string ExactArgument = "--serve-api-without-startup-maintenance";

    private ApiStartupExecutionMode(bool runStartupMaintenance)
    {
        RunStartupMaintenance = runStartupMaintenance;
    }

    public bool RunStartupMaintenance { get; }

    public static ApiStartupExecutionMode Parse(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var materialized = arguments.ToArray();
        if (materialized.Any(argument =>
                argument.StartsWith(ExactArgument, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(argument, ExactArgument, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("API_STARTUP_EXECUTION_MODE_ARGUMENT_INVALID");
        }

        var count = materialized.Count(argument =>
            string.Equals(argument, ExactArgument, StringComparison.Ordinal));
        if (count > 1)
        {
            throw new InvalidOperationException("API_STARTUP_EXECUTION_MODE_ARGUMENT_DUPLICATE");
        }

        if (count == 1
            && materialized.Any(argument =>
                !string.Equals(argument, ExactArgument, StringComparison.Ordinal)
                && argument.StartsWith("--run-", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("API_STARTUP_EXECUTION_MODE_OPERATIONAL_COMMAND_CONFLICT");
        }

        return new ApiStartupExecutionMode(runStartupMaintenance: count == 0);
    }

    public void EnsureNoOperationalCommandConflict(int operationalCommandCount)
    {
        if (operationalCommandCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(operationalCommandCount));
        }

        if (!RunStartupMaintenance && operationalCommandCount != 0)
        {
            throw new InvalidOperationException("API_STARTUP_EXECUTION_MODE_OPERATIONAL_COMMAND_CONFLICT");
        }
    }

    public void EnsureEnvironment(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        if (!RunStartupMaintenance
            && !string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("API_STARTUP_EXECUTION_MODE_ENVIRONMENT_NOT_ALLOWED");
        }
    }

    /// <summary>
    /// This is the production registration seam used by Program.cs and the container tests.
    /// Manifest providers remain available to the API, but the startup HTTP push is maintenance.
    /// </summary>
    public IServiceCollection AddModuleRegistrationHostedService(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (RunStartupMaintenance)
        {
            services.AddHostedService<ModuleRegistrationHostedService>();
        }

        return services;
    }
}
