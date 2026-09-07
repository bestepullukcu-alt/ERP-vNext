namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public static class LskuRetirementRequestRecoveryCommandLine
{
    public const string ExactArgument = "--run-lsku-retirement-recovery";
    public static bool IsRequested(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var all = arguments.ToArray();
        if (all.Any(x => x.StartsWith(ExactArgument, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(x, ExactArgument, StringComparison.Ordinal)))
            throw new InvalidOperationException("LSKU_RETIREMENT_RECOVERY_ARGUMENT_INVALID");
        if (all.Count(x => string.Equals(x, ExactArgument, StringComparison.Ordinal)) > 1)
            throw new InvalidOperationException("LSKU_RETIREMENT_RECOVERY_ARGUMENT_DUPLICATE");
        return all.Contains(ExactArgument, StringComparer.Ordinal);
    }
    public static Task<LskuRetirementRequestRecoveryRunResult> RunAsync(
        LskuRetirementRequestRecoveryRunner runner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runner); ct.ThrowIfCancellationRequested();
        return runner.RunCycleAsync(ct);
    }
}
