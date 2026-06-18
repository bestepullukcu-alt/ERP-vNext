using System.Net;

namespace Diten.AuthService.Infrastructure.Services.EmailTemplates;

// Invitation email: the recipient SETS their own password via a secure link.
// Deliberately NOT a temporary password — the link is single-use and expires in 7 days.
internal static class TenantUserInvitationEmailTemplate
{
    public static string Subject() => "You're invited — set your password";

    public static string Render(string setPasswordUrl)
    {
        var safeUrl = WebUtility.HtmlEncode(setPasswordUrl);
        return $"""
            <!doctype html>
            <html>
            <body style="font-family:Arial,Helvetica,sans-serif;background:#f5f5f9;margin:0;padding:24px;color:#384551;">
              <div style="max-width:560px;margin:0 auto;background:#fff;border-radius:8px;padding:28px;">
                <h2 style="margin:0 0 12px;">Welcome — set your password</h2>
                <p style="margin:0 0 20px;color:#697a8d;">An account has been created for you. Use the secure link below to set your own password and activate your account. The link expires in 7 days and can be used once.</p>
                <p style="margin:0 0 24px;"><a href="{safeUrl}" style="display:inline-block;background:#696cff;color:#fff;text-decoration:none;border-radius:6px;padding:12px 18px;font-weight:600;">Set your password</a></p>
                <p style="font-size:12px;color:#a1acb8;margin:0;">If you were not expecting this invitation, you can ignore this email.</p>
              </div>
            </body>
            </html>
            """;
    }
}
