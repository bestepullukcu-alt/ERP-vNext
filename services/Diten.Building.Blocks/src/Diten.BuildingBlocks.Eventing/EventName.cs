using System.Text.RegularExpressions;

namespace Diten.BuildingBlocks.Eventing;

public static partial class EventName
{
    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z][a-z0-9]*)*(?:\\.[a-z][a-z0-9]*(?:-[a-z][a-z0-9]*)*)+\\.v(?<version>[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex EventNameRegex();

    public static bool IsValid(string? eventName)
    {
        return !string.IsNullOrWhiteSpace(eventName) && EventNameRegex().IsMatch(eventName);
    }

    public static int GetVersion(string eventName)
    {
        var match = EventNameRegex().Match(eventName);
        if (!match.Success)
        {
            throw new EventValidationException($"EventName '{eventName}' must match '{{domain-or-aggregate}}.{{action}}.{{tense}}.v{{version}}'.");
        }

        return int.Parse(match.Groups["version"].Value);
    }

    public static void EnsureMatchesVersion(string eventName, int eventVersion)
    {
        if (eventVersion < 1)
        {
            throw new EventValidationException("EventVersion must be greater than zero.");
        }

        var suffixVersion = GetVersion(eventName);
        if (suffixVersion != eventVersion)
        {
            throw new EventValidationException($"EventVersion '{eventVersion}' must match EventName suffix 'v{suffixVersion}'.");
        }
    }
}
