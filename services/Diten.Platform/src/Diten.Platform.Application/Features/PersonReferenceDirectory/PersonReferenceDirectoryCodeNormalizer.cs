namespace Diten.Platform.Application.Features.PersonReferenceDirectory;

internal static class PersonReferenceDirectoryCodeNormalizer
{
    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
