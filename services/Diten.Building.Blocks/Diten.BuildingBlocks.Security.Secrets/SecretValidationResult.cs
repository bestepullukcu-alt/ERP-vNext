namespace Diten.BuildingBlocks.Security.Secrets;

public sealed class SecretValidationResult
{
    private SecretValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    public bool Succeeded => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; }

    public static SecretValidationResult Success() => new(Array.Empty<string>());

    public static SecretValidationResult Failure(IEnumerable<string> errors) =>
        new(errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToArray());
}
