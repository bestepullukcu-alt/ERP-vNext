using Diten.BuildingBlocks.Email;
using Diten.Platform.Domain.Entities.Notifications;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>What actually leaves: the cleaned subject, the framed body, and the name it is sent under.</summary>
public sealed record ComposedEmail(
    string Subject,
    string? BodyHtml,
    string? BodyText,
    string SenderName,
    string? ReplyToEmail,
    /// <summary>False when the template's body was already a whole document and was left exactly as written.</summary>
    bool Framed);

/// <summary>
/// BL-454 — puts a rendered notification into the e-mail shell at SEND time.
///
/// <para><b>Why at send time and not in the template.</b> 167 seeded templates, every tenant override and every
/// template an operator writes hold a body FRAGMENT. Framing here means none of them changes, all of them arrive
/// in the same card, and the frame lives in one file instead of in each row. What is stored on the dispatch
/// (<c>BodyHtmlPreview</c>) stays the fragment, so a retry frames again — including when all it has left is the
/// truncated preview.</para>
///
/// <para><b>A whole document is left alone.</b> A body that starts with <c>&lt;!doctype</c> or <c>&lt;html</c> is
/// an author's own complete e-mail; wrapping it would nest one document in another.</para>
/// </summary>
public interface IEmailShellComposer
{
    Task<ComposedEmail> ComposeAsync(
        Guid tenantId,
        NotificationTemplate? template,
        string? locale,
        string subject,
        string? bodyHtml,
        string? bodyText,
        IReadOnlyDictionary<string, object?>? variables,
        CancellationToken ct = default);

    /// <summary>The same framing with a caller-supplied identity — the template editor's preview has no tenant.</summary>
    ComposedEmail Compose(
        TenantEmailIdentity identity,
        NotificationTemplate? template,
        string? locale,
        string subject,
        string? bodyHtml,
        string? bodyText,
        IReadOnlyDictionary<string, object?>? variables);
}

public sealed class EmailShellComposer : IEmailShellComposer
{
    private static readonly IReadOnlyDictionary<string, object?> NoVariables = new Dictionary<string, object?>();

    private readonly ITenantEmailIdentityResolver _identity;
    private readonly ILogger<EmailShellComposer> _logger;

    public EmailShellComposer(ITenantEmailIdentityResolver identity, ILogger<EmailShellComposer> logger)
    {
        _identity = identity;
        _logger = logger;
    }

    public async Task<ComposedEmail> ComposeAsync(
        Guid tenantId,
        NotificationTemplate? template,
        string? locale,
        string subject,
        string? bodyHtml,
        string? bodyText,
        IReadOnlyDictionary<string, object?>? variables,
        CancellationToken ct = default)
    {
        TenantEmailIdentity identity;
        try
        {
            identity = await _identity.ResolveAsync(tenantId, ct) ?? TenantEmailIdentity.Platform;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The frame must never cost the message: an unreadable tenant record sends under the product's name.
            _logger.LogWarning(ex, "email.shell.identity_unavailable TenantId={TenantId}. Sending under the product name.", tenantId);
            identity = TenantEmailIdentity.Platform;
        }

        return Compose(identity, template, locale, subject, bodyHtml, bodyText, variables);
    }

    public ComposedEmail Compose(
        TenantEmailIdentity identity,
        NotificationTemplate? template,
        string? locale,
        string subject,
        string? bodyHtml,
        string? bodyText,
        IReadOnlyDictionary<string, object?>? variables)
    {
        // The language of the TEMPLATE that was found, so the frame's own sentences match the body inside it.
        var language = string.IsNullOrWhiteSpace(locale) ? identity.Language : locale;
        var cleanSubject = EmailHeaderText.CleanSubject(subject);
        var senderName = EmailSender.ComposeDisplayName(identity.SenderName, identity.DisplayName, language);

        if (IsWholeDocument(bodyHtml))
        {
            return new ComposedEmail(cleanSubject, bodyHtml, bodyText, senderName, identity.ReplyToEmail, Framed: false);
        }

        var values = variables ?? NoVariables;
        var shell = template?.Shell;
        var heading = string.IsNullOrWhiteSpace(shell?.HeadingTemplate)
            ? cleanSubject
            : TemplateTokens.Render(shell.HeadingTemplate, values).Trim();

        var rendered = EmailShell.Render(new EmailShellModel
        {
            Language = language,
            TenantDisplayName = identity.DisplayName,
            Heading = heading.Length > 0 ? heading : cleanSubject,
            // A text-only template still gets a card: its text becomes the paragraphs.
            Paragraphs = string.IsNullOrWhiteSpace(bodyHtml) ? SplitParagraphs(bodyText) : [],
            BodyHtmlFragment = string.IsNullOrWhiteSpace(bodyHtml) ? null : bodyHtml,
            BodyText = string.IsNullOrWhiteSpace(bodyHtml) ? null : bodyText,
            InfoRows = (shell?.InfoRows ?? [])
                .Select(row => new EmailShellInfoRow(row.Label, TemplateTokens.Render(row.ValueTemplate, values).Trim()))
                .ToList(),
            Action = ResolveAction(shell, values),
            Footnote = string.IsNullOrWhiteSpace(shell?.FootnoteTemplate)
                ? null
                : TemplateTokens.Render(shell.FootnoteTemplate, values),
            ReplyToEmail = identity.ReplyToEmail
        });

        return new ComposedEmail(cleanSubject, rendered.Html, rendered.Text, senderName, identity.ReplyToEmail, Framed: true);
    }

    public static bool IsWholeDocument(string? bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(bodyHtml))
        {
            return false;
        }

        var start = bodyHtml.AsSpan().TrimStart();
        return start.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase)
               || start.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static EmailShellAction? ResolveAction(NotificationTemplateShell? shell, IReadOnlyDictionary<string, object?> variables)
    {
        if (shell is null || string.IsNullOrWhiteSpace(shell.ActionLabel) || string.IsNullOrWhiteSpace(shell.ActionUrlVariable))
        {
            return null;
        }

        var lookup = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase);
        var url = lookup.TryGetValue(shell.ActionUrlVariable.Trim(), out var value) ? Convert.ToString(value) : null;
        // An unsafe or missing address is the shell's call: it draws no button rather than a dead or hostile one.
        return string.IsNullOrWhiteSpace(url) ? null : new EmailShellAction(shell.ActionLabel, url);
    }

    private static IReadOnlyList<string> SplitParagraphs(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
