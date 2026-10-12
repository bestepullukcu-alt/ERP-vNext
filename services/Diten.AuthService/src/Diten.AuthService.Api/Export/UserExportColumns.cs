using System.Globalization;
using System.Resources;
using Diten.AuthService.Application.DTOs;
using Diten.BuildingBlocks.ListExport;

namespace Diten.AuthService.Api.Export;

/// <summary>
/// BL-452 — the Users export's columns: the list DTO's field names (<c>email, firstName, lastName, roles, accountKind,
/// status</c>), nothing else. Headers and the status / account-kind values are the words the Users screen shows, in the
/// request culture (Resources/UserExport.*.resx, seven tenant languages).
/// </summary>
internal static class UserExportColumns
{
    public const string Screen = "users";

    private static readonly ResourceManager Text = new("Diten.AuthService.Api.Resources.UserExport", typeof(UserExportColumns).Assembly);

    public static string Label(string key, CultureInfo culture) => Text.GetString(key, culture) ?? key;

    // A value the resources do not name (a status a newer service invents) is written as it came, never as a resource key.
    private static string Value(string prefix, string value, CultureInfo culture) => Text.GetString(prefix + value, culture) ?? value;

    public static readonly ListExportColumnSet<UserDto> Set = new(
    [
        new("email", c => Label("Email", c), (u, _) => u.Email),
        new("firstName", c => Label("FirstName", c), (u, _) => u.FirstName),
        new("lastName", c => Label("LastName", c), (u, _) => u.LastName),
        // Several roles in one cell, "; "-separated: a comma would be the CSV separator's twin in a reader's eye.
        new("roles", c => Label("Roles", c), (u, _) => string.Join("; ", u.Roles ?? [])),
        new("accountKind", c => Label("AccountKind", c), (u, c) => Value("AccountKind", string.IsNullOrWhiteSpace(u.AccountKind) ? "Unknown" : u.AccountKind, c)),
        new("status", c => Label("Status", c), (u, c) => Value("Status", u.Status ?? (u.IsActive ? "Active" : "Inactive"), c))
    ]);
}
