namespace Diten.MdmService.Application.Contracts.Audit;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (M2) — ONE correlation id per request, normalised once: the caller's X-Correlation-Id when
/// it is a non-empty Guid, otherwise a fresh Guid; always written as Guid "D" (lower-case, hyphenated). The brand history
/// row and the central audit event read the SAME value, so the two can be joined.
/// </summary>
public interface IRequestCorrelation
{
    Guid CorrelationId { get; }
}

public static class RequestCorrelation
{
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>The caller's value as a Guid, or null when it is missing, empty or not a Guid.</summary>
    public static Guid? Parse(string? value)
        => Guid.TryParse(value?.Trim(), out var parsed) && parsed != Guid.Empty ? parsed : null;

    /// <summary>The stored form: Guid "D". A value that is not a Guid is replaced by a fresh one.</summary>
    public static string Normalize(string? value) => (Parse(value) ?? Guid.NewGuid()).ToString("D");
}
