using Microsoft.Extensions.Configuration.Json;

namespace Diten.MdmService.Api.Configuration;

/// <summary>
/// MOD-0033-FU02 §F.3 — Development secrets live in a git-ignored <c>appsettings.Development.local.json</c> next to
/// <c>appsettings.Development.json</c> (written by <c>scripts/dev/setup-service-trust.*</c>), not in tracked files.
///
/// <para>Development only; an absent file is normal. The file is inserted directly AFTER the tracked
/// <c>appsettings.Development.json</c>, so it overrides that file and nothing else: user secrets, environment
/// variables and command-line arguments keep their usual precedence over it.</para>
///
/// <para>The same file exists in the Auth, Platform and MDM API projects (they share no project that reads JSON
/// configuration); a test keeps the three copies identical.</para>
/// </summary>
public static class DevelopmentLocalSettings
{
    public const string FileName = "appsettings.Development.local.json";
    private const string TrackedDevelopmentFileName = "appsettings.Development.json";

    public static void AddDevelopmentLocalSettings(this IConfigurationBuilder configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        if (!environment.IsDevelopment()) return;

        var local = new JsonConfigurationSource { Path = FileName, Optional = true, ReloadOnChange = false };
        var tracked = configuration.Sources.OfType<JsonConfigurationSource>().LastOrDefault(source =>
            string.Equals(source.Path, TrackedDevelopmentFileName, StringComparison.OrdinalIgnoreCase));
        if (tracked is null)
        {
            configuration.Sources.Add(local);
            return;
        }

        configuration.Sources.Insert(configuration.Sources.IndexOf(tracked) + 1, local);
    }
}
