namespace Diten.BuildingBlocks.Email;

/// <summary>One line of the optional information table ("Due date" — "9 October 2026").</summary>
public sealed record EmailShellInfoRow(string Label, string Value);

/// <summary>The single action of an e-mail: a button, and the same address written out under it.</summary>
public sealed record EmailShellAction(string Label, string Url);

/// <summary>
/// BL-454 — what ONE e-mail says. Everything here is content; nothing here is appearance.
///
/// <para>Every string is plain text and is HTML-encoded by the shell, with one exception:
/// <see cref="BodyHtmlFragment"/>, which is markup a template author wrote and whose variables the template
/// renderer has already encoded.</para>
/// </summary>
public sealed record EmailShellModel
{
    /// <summary>The reader's language; decides the shell's own sentences and the direction of the page.</summary>
    public string? Language { get; init; }

    /// <summary>The tenant on the band. Null or blank: a platform e-mail, and the band carries the product name.</summary>
    public string? TenantDisplayName { get; init; }

    public string Heading { get; init; } = string.Empty;

    /// <summary>Body paragraphs as plain text.</summary>
    public IReadOnlyList<string> Paragraphs { get; init; } = [];

    /// <summary>Body as already-rendered markup (a notification template's fragment). Placed after the paragraphs.</summary>
    public string? BodyHtmlFragment { get; init; }

    /// <summary>The plain-text form of <see cref="BodyHtmlFragment"/>, for the text part.</summary>
    public string? BodyText { get; init; }

    public IReadOnlyList<EmailShellInfoRow> InfoRows { get; init; } = [];

    public EmailShellAction? Action { get; init; }

    /// <summary>A short note directly under the button ("The link is valid for 7 days…").</summary>
    public string? ActionNote { get; init; }

    /// <summary>The closing line inside the card ("You received this because…").</summary>
    public string? Footnote { get; init; }

    /// <summary>Where a reply goes. Blank: the footer does not mention replies at all.</summary>
    public string? ReplyToEmail { get; init; }
}

public sealed record EmailShellResult(string Html, string Text);
