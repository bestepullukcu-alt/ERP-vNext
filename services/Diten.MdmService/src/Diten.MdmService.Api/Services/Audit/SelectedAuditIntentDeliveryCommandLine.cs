namespace Diten.MdmService.Api.Services.Audit;

public static class SelectedAuditIntentDeliveryCommandLine
{
    public const string ExactArgument = "--selected-audit-intent-delivery";

    public static bool IsRequested(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var values = arguments.ToArray();
        if (values.Any(value => value.StartsWith("--selected-audit", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value, ExactArgument, StringComparison.Ordinal)))
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_ARGUMENT_INVALID");
        var count = values.Count(value => string.Equals(value, ExactArgument, StringComparison.Ordinal));
        if (count > 1) throw new InvalidOperationException("SELECTED_AUDIT_INTENT_ARGUMENT_DUPLICATE");
        if (count == 1 && values.Length != 1)
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_ARGUMENT_CONFLICT");
        return count == 1;
    }
}
