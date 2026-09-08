namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public static class GlobalProductRetirementRequestRecoveryCommandLine
{
    public const string ExactArgument = "--run-global-product-retirement-recovery";

    public static bool IsRequested(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var materialized = arguments.ToArray();
        if (materialized.Any(argument => argument.StartsWith(ExactArgument, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(argument, ExactArgument, StringComparison.Ordinal)))
            throw new InvalidOperationException("GLOBAL_PRODUCT_RETIREMENT_RECOVERY_ARGUMENT_INVALID");
        if (materialized.Count(argument => string.Equals(argument, ExactArgument, StringComparison.Ordinal)) > 1)
            throw new InvalidOperationException("GLOBAL_PRODUCT_RETIREMENT_RECOVERY_ARGUMENT_DUPLICATE");
        return materialized.Contains(ExactArgument, StringComparer.Ordinal);
    }

    public static Task<GlobalProductRetirementRequestRecoveryRunResult> RunAsync(
        GlobalProductRetirementRequestRecoveryRunner runner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        cancellationToken.ThrowIfCancellationRequested();
        return runner.RunCycleAsync(cancellationToken);
    }
}
