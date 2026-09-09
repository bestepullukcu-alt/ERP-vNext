namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public static class LskuIdentityWorkflowRecoveryCommandLine
{
    public const string ExactArgument = "--run-lsku-identity-workflow-recovery";
    public static bool IsRequested(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var values = arguments.ToArray();
        if (values.Any(value => value.StartsWith(ExactArgument, StringComparison.OrdinalIgnoreCase)
            && value != ExactArgument))
            throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_RECOVERY_ARGUMENT_INVALID");
        var count = values.Count(value => value == ExactArgument);
        if (count > 1) throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_RECOVERY_ARGUMENT_DUPLICATE");
        return count == 1;
    }

    public static async Task<LskuIdentityWorkflowRecoveryRunResult> RunAsync(
        LskuIdentityWorkflowRecoveryRunner runner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        cancellationToken.ThrowIfCancellationRequested();
        var result = await runner.RunCycleAsync(cancellationToken, requireEnabled: true);
        if (result.FailedCount > 0)
            throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_RECOVERY_FAILED");
        return result;
    }
}
