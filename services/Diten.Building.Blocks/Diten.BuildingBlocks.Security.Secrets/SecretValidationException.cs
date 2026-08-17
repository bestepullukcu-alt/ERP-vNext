namespace Diten.BuildingBlocks.Security.Secrets;

public sealed class SecretValidationException : InvalidOperationException
{
    public SecretValidationException(string serviceContext, IEnumerable<string> errors)
        : base(BuildMessage(serviceContext, errors))
    {
        ServiceContext = serviceContext;
        Errors = errors.ToArray();
    }

    public string ServiceContext { get; }
    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(string serviceContext, IEnumerable<string> errors)
    {
        var joined = string.Join("; ", errors);
        return $"Secret validation failed for {serviceContext}: {joined}";
    }
}
