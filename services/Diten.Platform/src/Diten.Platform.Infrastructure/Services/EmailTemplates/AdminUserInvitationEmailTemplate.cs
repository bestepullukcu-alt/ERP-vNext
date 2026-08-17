using System.Net;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Infrastructure.Services.EmailTemplates;

internal static class AdminUserInvitationEmailTemplate
{
    public static string Subject(Tenant tenant) =>
        $"Diten ERP invite - {tenant.DisplayName ?? tenant.Name}";

    public static string Render(Tenant tenant, TenantAdminUser adminUser, string loginUrl, string temporaryPassword)
    {
        var tenantName = Encode(tenant.DisplayName ?? tenant.Name);
        var adminName = Encode(string.IsNullOrWhiteSpace(adminUser.Name) ? adminUser.Email : adminUser.Name);
        var email = Encode(adminUser.Email);
        var safeLoginUrl = Encode(loginUrl);
        var safePassword = Encode(temporaryPassword);

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Diten ERP Invitation</title>
            </head>
            <body style="margin:0;padding:0;background:#f5f6fa;font-family:Arial,Helvetica,sans-serif;color:#566a7f;">
              <span style="display:none;font-size:1px;color:#f5f6fa;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;">
                You have been invited to Diten ERP.
              </span>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f5f6fa;padding:24px 0;">
                <tr>
                  <td align="center" style="padding:0 16px;">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:#ffffff;border:1px solid #e7e7ff;border-radius:8px;overflow:hidden;">
                      <tr>
                        <td style="padding:24px 28px;border-bottom:1px solid #eceef1;">
                          <div style="font-size:20px;font-weight:700;color:#384551;">Diten ERP</div>
                          <div style="margin-top:4px;font-size:13px;color:#697a8d;">Tenant admin invitation</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px;">
                          <p style="margin:0 0 16px;font-size:15px;line-height:1.6;">Hello {{adminName}},</p>
                          <p style="margin:0 0 20px;font-size:15px;line-height:1.6;">
                            You have been invited as an admin user for <strong style="color:#384551;">{{tenantName}}</strong>.
                          </p>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 24px;background:#f7f7fb;border:1px solid #eceef1;border-radius:8px;">
                            <tr>
                              <td style="padding:16px 18px;">
                                <div style="font-size:12px;text-transform:uppercase;letter-spacing:.4px;color:#a1acb8;margin-bottom:6px;">Email</div>
                                <div style="font-size:14px;color:#384551;word-break:break-word;">{{email}}</div>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:0 18px 16px;">
                                <div style="font-size:12px;text-transform:uppercase;letter-spacing:.4px;color:#a1acb8;margin-bottom:6px;">Temporary password</div>
                                <div style="font-size:16px;font-weight:700;color:#384551;letter-spacing:.3px;">{{safePassword}}</div>
                              </td>
                            </tr>
                          </table>
                          <p style="margin:0 0 24px;">
                            <a href="{{safeLoginUrl}}" style="display:inline-block;background:#696cff;color:#ffffff;text-decoration:none;font-size:14px;font-weight:600;padding:11px 18px;border-radius:6px;">
                              Open ERP Login
                            </a>
                          </p>
                          <p style="margin:0 0 8px;font-size:13px;line-height:1.6;color:#697a8d;">Login URL:</p>
                          <p style="margin:0 0 20px;font-size:13px;line-height:1.6;word-break:break-all;">
                            <a href="{{safeLoginUrl}}" style="color:#696cff;text-decoration:none;">{{safeLoginUrl}}</a>
                          </p>
                          <p style="margin:0;font-size:13px;line-height:1.6;color:#697a8d;">
                            Please sign in and change your password after your first login.
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:18px 28px;background:#fafafa;border-top:1px solid #eceef1;font-size:12px;line-height:1.5;color:#a1acb8;">
                          This invitation was sent by Diten ERP. If you were not expecting it, you can ignore this email.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
