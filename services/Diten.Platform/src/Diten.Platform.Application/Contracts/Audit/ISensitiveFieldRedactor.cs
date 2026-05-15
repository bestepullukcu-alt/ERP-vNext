namespace Diten.Platform.Application.Contracts.Audit;

public interface ISensitiveFieldRedactor
{
    object? Redact(object? value);
    IReadOnlyDictionary<string, object?> RedactDictionary(IReadOnlyDictionary<string, object?>? value);
}
