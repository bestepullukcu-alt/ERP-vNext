using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.PlatformAdministrators;

internal static class PlatformAdministratorParsing
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    public static string NormalizeUserName(string userName) => userName.Trim().ToLowerInvariant();

    public static ActorType ParseActorType(string value) =>
        Enum.Parse<ActorType>(value.Trim(), ignoreCase: false);

    public static AdministratorStatus ParseStatus(string value) =>
        Enum.Parse<AdministratorStatus>(value.Trim(), ignoreCase: false);

    public static AdministratorInvitationStatus ParseInvitationStatus(string value) =>
        Enum.Parse<AdministratorInvitationStatus>(value.Trim(), ignoreCase: false);

    public static AdministratorRole ParseRole(string value) =>
        Enum.Parse<AdministratorRole>(value.Trim(), ignoreCase: false);

    public static IReadOnlyList<TEnum> SplitEnums<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Enum.Parse<TEnum>(x, ignoreCase: false))
                .ToArray();

    public static IReadOnlyList<AdministratorRole> ParseRoles(IReadOnlyList<string> roles) =>
        roles.Select(ParseRole).Distinct().ToArray();

    public static IReadOnlyList<Guid> NormalizeTenantScope(IReadOnlyList<Guid>? tenantIds) =>
        tenantIds?.Where(x => x != Guid.Empty).Distinct().ToArray() ?? [];
}
