using System.Globalization;
using Diten.BuildingBlocks.Email;

namespace Diten.AuthService.Infrastructure.Services.EmailTemplates;

/// <summary>What the sign-in verification code e-mail says, in one language. <c>{0}</c> is the tenant's display name.</summary>
public sealed record VerificationCodeEmailTexts(
    string Subject,
    string Heading,
    string BodyForTenant,
    string Body,
    string CodeLabel,
    string ValidUntilLabel,
    string Footnote);

/// <summary>
/// The one-time code a tenant user receives when signing in with e-mail MFA (<c>MfaChallengeService</c>, called by the
/// tenant login only — the platform login has no MFA).
///
/// <para>BL-454 slice 2 stage D — content only, in the seven languages a tenant user can read, in the tenant's language;
/// the frame is the shared e-mail shell. Before this it was one English plain-text line. The code is in the body only —
/// never in the subject, which phones show on the lock screen and mail logs keep.</para>
/// </summary>
public static class VerificationCodeEmailTemplate
{
    public static readonly IReadOnlyList<string> Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly IReadOnlyDictionary<string, VerificationCodeEmailTexts> Texts =
        new Dictionary<string, VerificationCodeEmailTexts>(StringComparer.Ordinal)
        {
            ["en"] = new(
                "Your sign-in verification code",
                "Your verification code",
                "Use this code to finish signing in to {0}.",
                "Use this code to finish signing in.",
                "Verification code",
                "Valid until (UTC)",
                "If you did not try to sign in, change your password and tell your administrator. Never share this code with anyone."),
            ["tr"] = new(
                "Oturum açma doğrulama kodunuz",
                "Doğrulama kodunuz",
                "{0} oturumunuzu tamamlamak için bu kodu kullanın.",
                "Oturum açmayı tamamlamak için bu kodu kullanın.",
                "Doğrulama kodu",
                "Geçerlilik sonu (UTC)",
                "Oturum açmaya çalışan siz değilseniz parolanızı değiştirin ve yöneticinize bildirin. Bu kodu kimseyle paylaşmayın."),
            ["fr"] = new(
                "Votre code de vérification de connexion",
                "Votre code de vérification",
                "Utilisez ce code pour terminer votre connexion à {0}.",
                "Utilisez ce code pour terminer votre connexion.",
                "Code de vérification",
                "Valable jusqu'à (UTC)",
                "Si vous n'avez pas tenté de vous connecter, changez votre mot de passe et prévenez votre administrateur. Ne communiquez jamais ce code."),
            ["es"] = new(
                "Su código de verificación de inicio de sesión",
                "Su código de verificación",
                "Use este código para terminar de iniciar sesión en {0}.",
                "Use este código para terminar de iniciar sesión.",
                "Código de verificación",
                "Válido hasta (UTC)",
                "Si no intentó iniciar sesión, cambie su contraseña e informe a su administrador. No comparta nunca este código."),
            ["zh"] = new(
                "您的登录验证码",
                "您的验证码",
                "请使用此验证码完成 {0} 的登录。",
                "请使用此验证码完成登录。",
                "验证码",
                "有效期至（UTC）",
                "如果不是您本人尝试登录，请更改密码并告知管理员。切勿与任何人分享此验证码。"),
            ["ar"] = new(
                "رمز التحقق لتسجيل الدخول",
                "رمز التحقق الخاص بك",
                "استخدم هذا الرمز لإكمال تسجيل الدخول إلى {0}.",
                "استخدم هذا الرمز لإكمال تسجيل الدخول.",
                "رمز التحقق",
                "صالح حتى (UTC)",
                "إذا لم تكن أنت من حاول تسجيل الدخول، فغيّر كلمة المرور وأبلغ المسؤول. لا تشارك هذا الرمز مع أي شخص."),
            ["ru"] = new(
                "Ваш код подтверждения входа",
                "Ваш код подтверждения",
                "Введите этот код, чтобы завершить вход в {0}.",
                "Введите этот код, чтобы завершить вход.",
                "Код подтверждения",
                "Действует до (UTC)",
                "Если вы не пытались войти, смените пароль и сообщите администратору. Никому не сообщайте этот код.")
        };

    public static VerificationCodeEmailTexts TextsFor(string? language) => Texts[EmailShellTexts.NormalizeLanguage(language)];

    public static string Subject(string? language) => TextsFor(language).Subject;

    public static EmailShellResult Render(string? language, string? tenantDisplayName, string code, DateTime expiresAtUtc)
    {
        var texts = TextsFor(language);
        var tenant = EmailHeaderText.CleanDisplayName(tenantDisplayName);
        return EmailShell.Render(new EmailShellModel
        {
            Language = language,
            TenantDisplayName = tenant,
            Heading = texts.Heading,
            Paragraphs = [tenant.Length > 0 ? string.Format(texts.BodyForTenant, tenant) : texts.Body],
            InfoRows =
            [
                new EmailShellInfoRow(texts.CodeLabel, code),
                new EmailShellInfoRow(texts.ValidUntilLabel, expiresAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            ],
            Footnote = texts.Footnote
        });
    }
}
