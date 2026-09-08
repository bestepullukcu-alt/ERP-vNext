namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public static class GskuRetirementRequestRecoveryCommandLine
{
    public const string ExactArgument = "--run-gsku-retirement-request-recovery";
    public static bool IsRequested(IEnumerable<string> arguments)
    {
        var values = arguments.ToArray();
        if (values.Any(x => x.StartsWith(ExactArgument, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(x, ExactArgument, StringComparison.Ordinal)))
            throw new InvalidOperationException("GSKU_RETIREMENT_REQUEST_RECOVERY_ARGUMENT_INVALID");
        if (values.Count(x => string.Equals(x, ExactArgument, StringComparison.Ordinal)) > 1)
            throw new InvalidOperationException("GSKU_RETIREMENT_REQUEST_RECOVERY_ARGUMENT_DUPLICATE");
        return values.Contains(ExactArgument, StringComparer.Ordinal);
    }
    public static Task<GskuRetirementRequestRecoveryRunResult> RunAsync(GskuRetirementRequestRecoveryRunner runner,
        CancellationToken ct) => runner.RunCycleAsync(ct);
}
