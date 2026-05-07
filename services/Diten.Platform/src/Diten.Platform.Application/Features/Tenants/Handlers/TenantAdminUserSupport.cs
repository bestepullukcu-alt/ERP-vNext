using System.Text.RegularExpressions;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

internal static class TenantAdminUserSupport
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EmailSearchRegex = new(@"[^@\s]+@[^@\s]+\.[^@\s\).,]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static TenantAdminUserDto ToDto(TenantAdminUser user) =>
        new(user.Id, user.Name, user.Email, user.Status.ToString(), user.CreatedAt, user.InvitedAt);

    public static string NormalizeEmail(string email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        return normalized;
    }

    public static bool TryNormalizeEmail(string email, out string normalized)
    {
        normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        return EmailRegex.IsMatch(normalized);
    }

    public static string NormalizeName(string? name, string email)
    {
        var normalized = (name ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? email : normalized;
    }

    public static void AddActivity(Tenant tenant, string eventType, string message, string actor, DateTimeOffset now)
    {
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actor;
        tenant.ActivityTimeline.Add(new TenantActivityEvent
        {
            EventType = eventType,
            Message = message,
            At = now,
            Actor = actor
        });
    }

    public static bool EnsureInitialAdminUser(Tenant tenant)
    {
        if (tenant.AdminUsers.Count > 0)
        {
            return false;
        }

        var email = ExtractInitialAdminEmail(tenant);
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        tenant.AdminUsers.Add(new TenantAdminUser
        {
            Name = email,
            Email = email,
            Status = TenantAdminUserStatus.Invited,
            InvitedAt = tenant.CreatedAt
        });

        return true;
    }

    private static string? ExtractInitialAdminEmail(Tenant tenant)
    {
        var candidate = tenant.ProvisioningSteps
            .Select(step => step.Detail)
            .Concat(tenant.ActivityTimeline.Select(activity => activity.Message))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && EmailSearchRegex.Match(value).Success);

        return candidate == null ? null : NormalizeEmail(EmailSearchRegex.Match(candidate).Value);
    }
}
