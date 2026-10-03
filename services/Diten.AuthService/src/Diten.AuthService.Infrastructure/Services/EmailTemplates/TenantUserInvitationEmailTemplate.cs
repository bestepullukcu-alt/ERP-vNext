using Diten.BuildingBlocks.Email;

namespace Diten.AuthService.Infrastructure.Services.EmailTemplates;

/// <summary>What the invitation says, in one language. <c>{0}</c> is the tenant's display name / the reader's first name.</summary>
public sealed record TenantUserInvitationEmailTexts(
    string SubjectForTenant,
    string Subject,
    string Heading,
    string Greeting,
    string BodyForTenant,
    string Body,
    string Action,
    string ActionNote,
    string Footnote);

/// <summary>
/// Invitation email: the recipient SETS their own password via a secure link.
/// Deliberately NOT a temporary password — the link is single-use and expires in 7 days.
///
/// <para>BL-454 — this class supplies the CONTENT only, in the seven languages a tenant user can read; the frame it
/// arrives in is the shared e-mail shell (<see cref="EmailShell"/>), the same one Platform's notifications use.
/// Before this it was one hand-written English page.</para>
/// </summary>
public static class TenantUserInvitationEmailTemplate
{
    private static readonly IReadOnlyDictionary<string, TenantUserInvitationEmailTexts> Texts =
        new Dictionary<string, TenantUserInvitationEmailTexts>(StringComparer.Ordinal)
        {
            ["en"] = new(
                "Your {0} account is ready",
                "Your account is ready",
                "Your account is ready — set your password",
                "Hello {0},",
                "Your {0} administrator has created an account for you. Use the button below to set your own password and activate your account.",
                "Your administrator has created an account for you. Use the button below to set your own password and activate your account.",
                "Set my password",
                "The link is valid for 7 days and can be used only once.",
                "If you were not expecting this invitation, you can ignore this e-mail; the account cannot be used until a password is set."),
            ["tr"] = new(
                "{0} hesabınız hazır",
                "Hesabınız hazır",
                "Hesabınız hazır, parolanızı belirleyin",
                "Merhaba {0},",
                "{0} yöneticiniz sizin için bir hesap açtı. Aşağıdaki düğmeyle kendi parolanızı belirleyip hesabınızı etkinleştirebilirsiniz.",
                "Yöneticiniz sizin için bir hesap açtı. Aşağıdaki düğmeyle kendi parolanızı belirleyip hesabınızı etkinleştirebilirsiniz.",
                "Parolamı belirle",
                "Bağlantı 7 gün geçerlidir ve yalnız bir kez kullanılabilir.",
                "Bu daveti beklemiyorsanız e-postayı yok sayabilirsiniz; hesap, parola belirlenmeden kullanılamaz."),
            ["fr"] = new(
                "Votre compte {0} est prêt",
                "Votre compte est prêt",
                "Votre compte est prêt : définissez votre mot de passe",
                "Bonjour {0},",
                "Votre administrateur {0} a créé un compte pour vous. Utilisez le bouton ci-dessous pour définir votre propre mot de passe et activer votre compte.",
                "Votre administrateur a créé un compte pour vous. Utilisez le bouton ci-dessous pour définir votre propre mot de passe et activer votre compte.",
                "Définir mon mot de passe",
                "Le lien est valable 7 jours et ne peut être utilisé qu'une seule fois.",
                "Si vous n'attendiez pas cette invitation, vous pouvez ignorer cet e-mail ; le compte reste inutilisable tant qu'aucun mot de passe n'est défini."),
            ["es"] = new(
                "Su cuenta de {0} está lista",
                "Su cuenta está lista",
                "Su cuenta está lista: establezca su contraseña",
                "Hola, {0}:",
                "Su administrador de {0} ha creado una cuenta para usted. Use el botón siguiente para establecer su propia contraseña y activar la cuenta.",
                "Su administrador ha creado una cuenta para usted. Use el botón siguiente para establecer su propia contraseña y activar la cuenta.",
                "Establecer mi contraseña",
                "El enlace es válido durante 7 días y solo puede usarse una vez.",
                "Si no esperaba esta invitación, puede ignorar este correo; la cuenta no puede usarse hasta que se establezca una contraseña."),
            ["zh"] = new(
                "您的 {0} 账户已就绪",
                "您的账户已就绪",
                "您的账户已就绪，请设置密码",
                "{0}，您好：",
                "{0} 的管理员已为您创建账户。请点击下方按钮设置您自己的密码并激活账户。",
                "管理员已为您创建账户。请点击下方按钮设置您自己的密码并激活账户。",
                "设置我的密码",
                "链接 7 天内有效，且只能使用一次。",
                "如果您并未预期收到此邀请，可以忽略此邮件；在设置密码之前，该账户无法使用。"),
            ["ar"] = new(
                "حسابك في {0} جاهز",
                "حسابك جاهز",
                "حسابك جاهز، عيّن كلمة المرور",
                "مرحبًا {0}،",
                "أنشأ مسؤول {0} حسابًا لك. استخدم الزر أدناه لتعيين كلمة المرور الخاصة بك وتفعيل حسابك.",
                "أنشأ المسؤول حسابًا لك. استخدم الزر أدناه لتعيين كلمة المرور الخاصة بك وتفعيل حسابك.",
                "تعيين كلمة المرور",
                "الرابط صالح لمدة 7 أيام ويمكن استخدامه مرة واحدة فقط.",
                "إذا لم تكن تتوقع هذه الدعوة فيمكنك تجاهل هذه الرسالة؛ لا يمكن استخدام الحساب قبل تعيين كلمة المرور."),
            ["ru"] = new(
                "Ваша учётная запись {0} готова",
                "Ваша учётная запись готова",
                "Учётная запись готова — задайте пароль",
                "Здравствуйте, {0}!",
                "Администратор {0} создал для вас учётную запись. Нажмите кнопку ниже, чтобы задать собственный пароль и активировать её.",
                "Администратор создал для вас учётную запись. Нажмите кнопку ниже, чтобы задать собственный пароль и активировать её.",
                "Задать пароль",
                "Ссылка действует 7 дней и может быть использована только один раз.",
                "Если вы не ожидали этого приглашения, просто проигнорируйте письмо: пока пароль не задан, учётной записью пользоваться нельзя.")
        };

    /// <summary>
    /// BL-454 — an administrator's "Reset password" for an account that is already in use. Same link, same token,
    /// same lifetime as the invitation; different words, because nobody was invited. Turkish says "parola", as the
    /// Users screen does.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, TenantUserInvitationEmailTexts> ResetTexts =
        new Dictionary<string, TenantUserInvitationEmailTexts>(StringComparer.Ordinal)
        {
            ["en"] = new(
                "Reset your {0} password",
                "Reset your password",
                "Set a new password",
                "Hello {0},",
                "An administrator of {0} has reset the password of your account. Use the button below to set a new one.",
                "An administrator has reset the password of your account. Use the button below to set a new one.",
                "Set a new password",
                "The link is valid for 7 days and can be used only once. Your old password no longer works.",
                "If you did not expect this, contact your administrator."),
            ["tr"] = new(
                "{0} parolanızı yenileyin",
                "Parolanızı yenileyin",
                "Yeni parolanızı belirleyin",
                "Merhaba {0},",
                "{0} yöneticiniz hesabınızın parolasını sıfırladı. Aşağıdaki düğmeyle yeni bir parola belirleyin.",
                "Yöneticiniz hesabınızın parolasını sıfırladı. Aşağıdaki düğmeyle yeni bir parola belirleyin.",
                "Yeni parola belirle",
                "Bağlantı 7 gün geçerlidir ve yalnız bir kez kullanılabilir. Eski parolanız artık geçerli değil.",
                "Bunu beklemiyorsanız yöneticinize başvurun."),
            ["fr"] = new(
                "Réinitialisez votre mot de passe {0}",
                "Réinitialisez votre mot de passe",
                "Définissez un nouveau mot de passe",
                "Bonjour {0},",
                "Un administrateur de {0} a réinitialisé le mot de passe de votre compte. Utilisez le bouton ci-dessous pour en définir un nouveau.",
                "Un administrateur a réinitialisé le mot de passe de votre compte. Utilisez le bouton ci-dessous pour en définir un nouveau.",
                "Définir un nouveau mot de passe",
                "Le lien est valable 7 jours et ne peut être utilisé qu'une seule fois. Votre ancien mot de passe ne fonctionne plus.",
                "Si vous ne vous y attendiez pas, contactez votre administrateur."),
            ["es"] = new(
                "Restablezca su contraseña de {0}",
                "Restablezca su contraseña",
                "Establezca una nueva contraseña",
                "Hola, {0}:",
                "Un administrador de {0} ha restablecido la contraseña de su cuenta. Use el botón siguiente para establecer una nueva.",
                "Un administrador ha restablecido la contraseña de su cuenta. Use el botón siguiente para establecer una nueva.",
                "Establecer nueva contraseña",
                "El enlace es válido durante 7 días y solo puede usarse una vez. Su contraseña anterior ya no funciona.",
                "Si no esperaba esto, póngase en contacto con su administrador."),
            ["zh"] = new(
                "重置您的 {0} 密码",
                "重置您的密码",
                "设置新密码",
                "{0}，您好：",
                "{0} 的管理员已重置您账户的密码。请点击下方按钮设置新密码。",
                "管理员已重置您账户的密码。请点击下方按钮设置新密码。",
                "设置新密码",
                "链接 7 天内有效，且只能使用一次。您的旧密码已失效。",
                "如果这不是您预期的操作，请联系您的管理员。"),
            ["ar"] = new(
                "أعد تعيين كلمة مرورك في {0}",
                "أعد تعيين كلمة مرورك",
                "عيّن كلمة مرور جديدة",
                "مرحبًا {0}،",
                "أعاد مسؤول {0} تعيين كلمة مرور حسابك. استخدم الزر أدناه لتعيين كلمة مرور جديدة.",
                "أعاد المسؤول تعيين كلمة مرور حسابك. استخدم الزر أدناه لتعيين كلمة مرور جديدة.",
                "تعيين كلمة مرور جديدة",
                "الرابط صالح لمدة 7 أيام ويمكن استخدامه مرة واحدة فقط. لم تعد كلمة مرورك القديمة صالحة.",
                "إذا لم تكن تتوقع ذلك، فتواصل مع المسؤول."),
            ["ru"] = new(
                "Сброс пароля {0}",
                "Сброс пароля",
                "Задайте новый пароль",
                "Здравствуйте, {0}!",
                "Администратор {0} сбросил пароль вашей учётной записи. Нажмите кнопку ниже, чтобы задать новый.",
                "Администратор сбросил пароль вашей учётной записи. Нажмите кнопку ниже, чтобы задать новый.",
                "Задать новый пароль",
                "Ссылка действует 7 дней и может быть использована только один раз. Старый пароль больше не действует.",
                "Если вы этого не ожидали, обратитесь к администратору.")
        };

    public static TenantUserInvitationEmailTexts ResetTextsFor(string? language) =>
        ResetTexts[EmailShellTexts.NormalizeLanguage(language)];

    public static TenantUserInvitationEmailTexts TextsFor(string? language) =>
        Texts[EmailShellTexts.NormalizeLanguage(language)];

    public static string Subject(string? language, string? tenantDisplayName, bool isPasswordReset = false)
    {
        var texts = isPasswordReset ? ResetTextsFor(language) : TextsFor(language);
        var tenant = EmailHeaderText.CleanDisplayName(tenantDisplayName);
        return EmailHeaderText.CleanSubject(tenant.Length > 0 ? string.Format(texts.SubjectForTenant, tenant) : texts.Subject);
    }

    public static EmailShellResult Render(
        string? language, string? tenantDisplayName, string? recipientFirstName, string setPasswordUrl, string? replyToEmail,
        bool isPasswordReset = false)
    {
        var texts = isPasswordReset ? ResetTextsFor(language) : TextsFor(language);
        var tenant = EmailHeaderText.CleanDisplayName(tenantDisplayName);
        // The name goes into the BODY, not a header: only control characters go; markup is encoded by the shell.
        var name = EmailHeaderText.Clean(recipientFirstName, EmailHeaderText.MaxDisplayNameLength);
        var body = tenant.Length > 0 ? string.Format(texts.BodyForTenant, tenant) : texts.Body;

        return EmailShell.Render(new EmailShellModel
        {
            Language = language,
            TenantDisplayName = tenant,
            Heading = texts.Heading,
            // No name on record: the sentence stands on its own rather than greeting nobody.
            Paragraphs = [name.Length > 0 ? string.Format(texts.Greeting, name) + " " + body : body],
            Action = new EmailShellAction(texts.Action, setPasswordUrl),
            ActionNote = texts.ActionNote,
            Footnote = texts.Footnote,
            ReplyToEmail = replyToEmail
        });
    }
}
