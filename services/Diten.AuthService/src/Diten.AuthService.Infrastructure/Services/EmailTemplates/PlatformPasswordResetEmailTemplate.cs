using System.Globalization;
using Diten.BuildingBlocks.Email;

namespace Diten.AuthService.Infrastructure.Services.EmailTemplates;

/// <summary>What the platform administrator's set-password e-mail says, in one language.
/// <c>{0}</c> in <see cref="ActionNoteUntil"/> is the link's expiry time.</summary>
public sealed record PlatformPasswordResetEmailTexts(
    string Subject,
    string Heading,
    string Body,
    string Action,
    string ActionNoteUntil,
    string ActionNote,
    string Footnote);

/// <summary>
/// The platform administrator's set-password link: the self-service "forgot password" (valid 1 hour) and the links an
/// administrator issues — a new platform administrator, a re-invitation, an administrator's reset (valid 24 hours).
///
/// <para>BL-454 slice 2 stage D — content only, in the two languages of the platform console (en, tr); the frame is the
/// shared e-mail shell (<see cref="EmailShell"/>). Before this it was one hand-written English page that said "expires in
/// one hour" for every link, the 24-hour ones too. The expiry is now the one stored with the link.</para>
///
/// <para>⚠ The words fit every path: they never claim the old password still works (after an administrator's reset it
/// does not — BL-529) nor that it stopped working (after "forgot password" it still does).</para>
/// </summary>
public static class PlatformPasswordResetEmailTemplate
{
    public static readonly IReadOnlyList<string> Languages = ["en", "tr"];

    private static readonly IReadOnlyDictionary<string, PlatformPasswordResetEmailTexts> Texts =
        new Dictionary<string, PlatformPasswordResetEmailTexts>(StringComparer.Ordinal)
        {
            ["en"] = new(
                "Set a new password for your Di10 platform account",
                "Set a new password",
                "A link to set a new password has been issued for your Di10 platform account. Use the button below to set it.",
                "Set new password",
                "The link is valid until {0} (UTC) and can be used only once.",
                "The link can be used only once.",
                "If you did not expect this e-mail, tell your platform administrator."),
            ["tr"] = new(
                "Di10 platform hesabınız için yeni parola belirleyin",
                "Yeni parola belirleyin",
                "Di10 platform hesabınız için yeni parola belirleme bağlantısı oluşturuldu. Aşağıdaki düğmeyle yeni parolanızı belirleyin.",
                "Yeni parola belirle",
                "Bağlantı {0} (UTC) saatine kadar geçerlidir ve yalnız bir kez kullanılabilir.",
                "Bağlantı yalnız bir kez kullanılabilir.",
                "Bu e-postayı beklemiyorsanız platform yöneticinize bildirin.")
        };

    /// <summary>The platform console's two languages; anything else reads English.</summary>
    public static string LanguageOf(string? language)
    {
        var normalized = EmailShellTexts.NormalizeLanguage(language);
        return Texts.ContainsKey(normalized) ? normalized : "en";
    }

    public static PlatformPasswordResetEmailTexts TextsFor(string? language) => Texts[LanguageOf(language)];

    public static string Subject(string? language = null) => TextsFor(language).Subject;

    /// <param name="expiresAtUtc">When the link stops working, as stored with it; null when it could not be read.</param>
    public static EmailShellResult Render(string? language, string resetUrl, DateTime? expiresAtUtc)
    {
        var texts = TextsFor(language);
        return EmailShell.Render(new EmailShellModel
        {
            Language = LanguageOf(language),
            Heading = texts.Heading,
            Paragraphs = [texts.Body],
            Action = new EmailShellAction(texts.Action, resetUrl),
            ActionNote = expiresAtUtc is { } until
                ? string.Format(texts.ActionNoteUntil, until.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                : texts.ActionNote,
            Footnote = texts.Footnote
        });
    }
}
