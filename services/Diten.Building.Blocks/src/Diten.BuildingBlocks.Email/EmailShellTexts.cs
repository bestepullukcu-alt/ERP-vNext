namespace Diten.BuildingBlocks.Email;

/// <summary>
/// BL-454 — the shell's OWN words, in the seven languages a tenant reader can have. An e-mail supplies its content;
/// these are the sentences that belong to the frame around it.
///
/// <para><c>{0}</c> is the tenant's display name and <c>{1}</c> the product name, except where noted.</para>
/// </summary>
public sealed record EmailShellTexts(
    string Language,
    bool RightToLeft,
    /// <summary>Sender name: "{tenant} (via {product})".</summary>
    string SenderVia,
    /// <summary>Above the plain address under the button.</summary>
    string ActionFallback,
    /// <summary>Footer of a tenant e-mail.</summary>
    string FooterOnBehalf,
    /// <summary>Footer of a platform e-mail; <c>{0}</c> is the product name.</summary>
    string FooterPlatform,
    /// <summary>Footer, only when a reply address exists; <c>{0}</c> is that address.</summary>
    string FooterReply)
{
    public const string DefaultLanguage = "en";

    public static IReadOnlyList<string> SupportedLanguages { get; } = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly IReadOnlyDictionary<string, EmailShellTexts> All = new Dictionary<string, EmailShellTexts>(StringComparer.Ordinal)
    {
        ["en"] = new("en", false,
            "{0} (via {1})",
            "If the button does not work, paste this address into your browser:",
            "This e-mail was sent by {1} on behalf of {0}.",
            "This e-mail was sent by {0}.",
            "Your reply goes to {0}."),
        ["tr"] = new("tr", false,
            "{0} ({1} üzerinden)",
            "Düğme çalışmıyorsa bu adresi tarayıcınıza yapıştırın:",
            "Bu e-posta {0} adına {1} üzerinden gönderildi.",
            "Bu e-posta {0} tarafından gönderildi.",
            "Yanıtınız {0} adresine ulaşır."),
        ["fr"] = new("fr", false,
            "{0} (via {1})",
            "Si le bouton ne fonctionne pas, collez cette adresse dans votre navigateur :",
            "Cet e-mail a été envoyé par {1} pour le compte de {0}.",
            "Cet e-mail a été envoyé par {0}.",
            "Votre réponse sera transmise à {0}."),
        ["es"] = new("es", false,
            "{0} (a través de {1})",
            "Si el botón no funciona, pegue esta dirección en su navegador:",
            "Este correo fue enviado por {1} en nombre de {0}.",
            "Este correo fue enviado por {0}.",
            "Su respuesta llegará a {0}."),
        ["zh"] = new("zh", false,
            "{0}（通过 {1}）",
            "如果按钮无法使用，请将此地址粘贴到浏览器中：",
            "此邮件由 {1} 代表 {0} 发送。",
            "此邮件由 {0} 发送。",
            "您的回复将发送至 {0}。"),
        ["ar"] = new("ar", true,
            "{0} (عبر {1})",
            "إذا لم يعمل الزر، الصق هذا العنوان في متصفحك:",
            "أُرسلت هذه الرسالة عبر {1} نيابةً عن {0}.",
            "أُرسلت هذه الرسالة من {0}.",
            "سيصل ردّك إلى {0}."),
        ["ru"] = new("ru", false,
            "{0} (через {1})",
            "Если кнопка не работает, вставьте этот адрес в адресную строку браузера:",
            "Это письмо отправлено через {1} от имени {0}.",
            "Это письмо отправлено {0}.",
            "Ваш ответ будет доставлен на адрес {0}.")
    };

    /// <summary>
    /// <c>tr-TR</c> → <c>tr</c>; a language outside the seven → English. Never throws: the shell sits in front of
    /// every e-mail, and an unknown language must cost a translation, not the message.
    /// </summary>
    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultLanguage;
        }

        var value = language.Trim().ToLowerInvariant();
        var separator = value.IndexOfAny(['-', '_']);
        if (separator > 0)
        {
            value = value[..separator];
        }

        return All.ContainsKey(value) ? value : DefaultLanguage;
    }

    public static EmailShellTexts For(string? language) => All[NormalizeLanguage(language)];
}
