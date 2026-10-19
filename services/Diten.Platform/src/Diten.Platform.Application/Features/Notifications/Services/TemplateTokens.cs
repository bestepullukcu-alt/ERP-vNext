using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>
/// The <c>{{Variable}}</c> substitution every part of a notification template goes through — subject, body, and
/// (BL-454) the shell's heading, table values and footnote. One implementation, so a token means the same thing in
/// each of them. A variable nobody supplied renders as an empty string.
/// </summary>
public static partial class TemplateTokens
{
    public static string Render(
        string template, IReadOnlyDictionary<string, object?> variables, Func<string, string>? encode = null) =>
        TokenRegex().Replace(template, match =>
        {
            var key = match.Groups["name"].Value.Trim();
            var text = variables.TryGetValue(key, out var value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
            return encode is null ? text : encode(text);
        });

    [GeneratedRegex("\\{\\{\\s*(?<name>[A-Za-z][A-Za-z0-9_.]*)\\s*\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
