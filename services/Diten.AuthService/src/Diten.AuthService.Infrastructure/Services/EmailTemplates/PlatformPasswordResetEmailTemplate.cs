using System.Net;

namespace Diten.AuthService.Infrastructure.Services.EmailTemplates;

internal static class PlatformPasswordResetEmailTemplate
{
    public static string Subject() => "Reset your Di10 platform password";

    public static string Render(string resetUrl)
    {
        var safeUrl = WebUtility.HtmlEncode(resetUrl);
        return $"""
            <!doctype html>
            <html>
            <body style="font-family:Arial,Helvetica,sans-serif;background:#f5f5f9;margin:0;padding:24px;color:#384551;">
              <div style="max-width:560px;margin:0 auto;background:#fff;border-radius:8px;padding:28px;">
                <h2 style="margin:0 0 12px;">Reset your platform password</h2>
                <p style="margin:0 0 20px;color:#697a8d;">Use the secure link below to set a new password. The link expires in one hour and can be used once.</p>
                <p style="margin:0 0 24px;"><a href="{safeUrl}" style="display:inline-block;background:#696cff;color:#fff;text-decoration:none;border-radius:6px;padding:12px 18px;font-weight:600;">Reset password</a></p>
                <p style="font-size:12px;color:#a1acb8;margin:0;">If you did not request this, you can ignore this email.</p>
              </div>
            </body>
            </html>
            """;
    }
}
