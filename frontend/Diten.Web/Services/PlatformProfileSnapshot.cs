namespace Diten.Web.Services;

public sealed record PlatformProfileSnapshot(
    string DisplayName,
    string Initials,
    string? Email,
    string? ActorType,
    string? FirstName,
    string? LastName,
    IReadOnlyList<string> Roles);
