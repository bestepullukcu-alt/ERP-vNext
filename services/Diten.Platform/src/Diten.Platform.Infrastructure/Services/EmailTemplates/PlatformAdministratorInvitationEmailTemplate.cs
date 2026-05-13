using System.Net;

namespace Diten.Platform.Infrastructure.Services.EmailTemplates;

internal static class PlatformAdministratorInvitationEmailTemplate
{
    public static string Subject() => "Your Di10 platform admin account";

    public static string Render(string displayName, string userName, string email, string loginUrl, string temporaryPassword)
    {
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(displayName) ? "Platform administrator" : displayName);
        var safeUserName = WebUtility.HtmlEncode(userName);
        var safeEmail = WebUtility.HtmlEncode(email);
        var safeLoginUrl = WebUtility.HtmlEncode(loginUrl);
        var safePassword = WebUtility.HtmlEncode(temporaryPassword);

        return $"""
            <!doctype html>
            <html>
            <body style="font-family:Arial,Helvetica,sans-serif;background:#f5f5f9;margin:0;padding:24px;color:#384551;">
              <div style="max-width:560px;margin:0 auto;background:#fff;border-radius:8px;padding:28px;">
                <h2 style="margin:0 0 12px;">Welcome, {safeName}</h2>
                <p style="margin:0 0 20px;color:#697a8d;">Your Di10 platform administrator account has been created. Sign in with the temporary password below; you will be asked to set a new password immediately.</p>
                <div style="background:#f7f7fb;border-radius:6px;padding:16px;margin-bottom:12px;">
                  <div style="font-size:12px;text-transform:uppercase;letter-spacing:.4px;color:#a1acb8;margin-bottom:6px;">Username</div>
                  <div style="font-size:16px;font-weight:700;color:#384551;">{safeUserName}</div>
                  <div style="font-size:12px;color:#697a8d;margin-top:8px;">You can also sign in with {safeEmail}.</div>
                </div>
                <div style="background:#f7f7fb;border-radius:6px;padding:16px;margin-bottom:20px;">
                  <div style="font-size:12px;text-transform:uppercase;letter-spacing:.4px;color:#a1acb8;margin-bottom:6px;">Temporary password</div>
                  <div style="font-size:18px;font-weight:700;color:#384551;letter-spacing:.3px;">{safePassword}</div>
                </div>
                <p style="margin:0 0 24px;"><a href="{safeLoginUrl}" style="display:inline-block;background:#696cff;color:#fff;text-decoration:none;border-radius:6px;padding:12px 18px;font-weight:600;">Open platform login</a></p>
                <p style="font-size:12px;color:#a1acb8;margin:0;">This temporary password expires in seven days.</p>
              </div>
            </body>
            </html>
            """;
    }
}
