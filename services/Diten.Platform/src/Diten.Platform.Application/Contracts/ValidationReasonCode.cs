using System.Text;
using System.Text.RegularExpressions;
using FluentValidation.Results;

namespace Diten.Platform.Application.Contracts;

/// <summary>
/// The stable reason code for one FluentValidation failure (BL-040).
///
/// <para><b>Why a code at all.</b> The reason-code bridge turns a stable code into a sentence in the reader's own
/// language through the frontend resx (seven languages). A failure that carries no code arrives as English server
/// text that nothing can translate — it passes every l10n gate we have and still shows English on screen. Every
/// FluentValidation failure on this platform was in that state.</para>
///
/// <para><b>Why DERIVED rather than hand-written on every rule.</b> Requiring a curated code per rule would mean
/// 150 validators had to be edited before a single error carried a code, so the platform-wide defect would stay
/// open until the last module was migrated. Deriving one means every validator that exists today already answers
/// with a code, and a rule that wants a curated one says so with <c>.WithErrorCode(...)</c>.</para>
///
/// <para><b>The stability rule, which is the whole point.</b> The code is built from the FIELD and the RULE —
/// never from the message. An editor improving an English sentence must not silently unmap every translation of
/// it. Renaming the field or changing which rule fires DOES change the code, and that is correct: it is a
/// different failure.</para>
/// </summary>
public static class ValidationReasonCode
{
    /// <summary>Prefix that marks a code this class derived, as opposed to one a rule chose for itself.</summary>
    public const string DerivedPrefix = "VALIDATION";

    /// <summary>
    /// FluentValidation seeds <see cref="ValidationFailure.ErrorCode"/> with the validator's type name
    /// (<c>NotEmptyValidator</c>, <c>MaximumLengthValidator</c>, …). That suffix is how a DEFAULT is told apart
    /// from a code somebody wrote on purpose, so it is matched here rather than guessed at each call site.
    /// </summary>
    private const string DefaultErrorCodeSuffix = "Validator";

    /// <summary>Collection indexers (<c>Items[0].Name</c>) — the index is data, not part of the rule's identity.</summary>
    private static readonly Regex Indexer = new(@"\[\d+\]", RegexOptions.Compiled);

    /// <summary>Word boundaries inside a PascalCase segment, so <c>MaximumLength</c> reads as MAXIMUM_LENGTH.</summary>
    private static readonly Regex PascalBoundary = new(@"(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

    public static string? From(ValidationFailure? failure)
    {
        if (failure is null)
        {
            return null;
        }

        var errorCode = failure.ErrorCode?.Trim();

        /*
         * A CURATED code is used verbatim — not prefixed, not reshaped. Codes like REVIEW_REVIEWER_REQUIRED are
         * already mapped in the frontend bridge, so a rule moved out of a handler and into a validator has to keep
         * answering with the identical string or the mapping silently stops matching.
         */
        if (!string.IsNullOrEmpty(errorCode) && !errorCode.EndsWith(DefaultErrorCodeSuffix, StringComparison.Ordinal))
        {
            return errorCode;
        }

        var field = Segment(failure.PropertyName);
        var rule = Segment(TrimDefaultSuffix(errorCode));

        var builder = new StringBuilder(DerivedPrefix);
        // A rule-level failure (a custom rule, a cross-field check) carries no property name. Returning nothing
        // there would leave exactly the interesting rules code-less, so the segment is simply omitted.
        if (field.Length > 0) { builder.Append('_').Append(field); }
        if (rule.Length > 0) { builder.Append('_').Append(rule); }

        return builder.ToString();
    }

    private static string? TrimDefaultSuffix(string? errorCode) =>
        string.IsNullOrEmpty(errorCode) || !errorCode.EndsWith(DefaultErrorCodeSuffix, StringComparison.Ordinal)
            ? errorCode
            : errorCode[..^DefaultErrorCodeSuffix.Length];

    /// <summary>SCREAMING_SNAKE, from a dotted PascalCase path. Mechanical on purpose: the resx has to mirror it.</summary>
    private static string Segment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutIndexers = Indexer.Replace(value, string.Empty);
        var separated = PascalBoundary.Replace(withoutIndexers, "_").Replace('.', '_');

        var parts = separated.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join('_', parts).ToUpperInvariant();
    }
}
