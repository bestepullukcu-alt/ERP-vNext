namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public static class FirstGskuIdentityWorkflowRecoveryCommandLine
{
    public const string ExactArgument = "--run-first-gsku-identity-workflow-recovery";

    public static bool IsRequested(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var materialized = arguments.ToArray();
        if (materialized.Any(argument =>
                argument.StartsWith(ExactArgument, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(argument, ExactArgument, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("FIRST_GSKU_IDENTITY_WORKFLOW_RECOVERY_ARGUMENT_INVALID");
        }

        var count = materialized.Count(argument =>
            string.Equals(argument, ExactArgument, StringComparison.Ordinal));
        if (count > 1)
        {
            throw new InvalidOperationException("FIRST_GSKU_IDENTITY_WORKFLOW_RECOVERY_ARGUMENT_DUPLICATE");
        }

        return count == 1;
    }

    public static Task<FirstGskuIdentityWorkflowRecoveryRunResult> RunAsync(
        FirstGskuIdentityWorkflowRecoveryRunner runner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        cancellationToken.ThrowIfCancellationRequested();
        return runner.RunCycleAsync(cancellationToken);
    }
}
