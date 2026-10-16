using Microsoft.Extensions.Configuration.Json;

namespace Diten.MdmService.Api.Configuration;

/// <summary>
/// MOD-0033-FU02 §F.3 — Development secrets live in a git-ignored <c>appsettings.Development.local.json</c> next to
/// <c>appsettings.Development.json</c> (written by <c>scripts/dev/setup-service-trust.*</c>), not in tracked files.
///
/// <para>Development only; an absent file is normal. The file is inserted directly AFTER the tracked
/// <c>appsettings.Development.json</c>. It therefore outranks that file and whatever that file itself outranks
/// (<c>appsettings.json</c> and the host-level defaults added before it), and nothing else: every source the host
/// adds after the tracked Development file — user secrets, environment variables, command-line arguments — keeps
/// its precedence over it.</para>
///
/// <para>FAIL-SAFE POSITION. If the tracked Development file is not among the sources, the place between it and the
/// environment variables cannot be found, and the local file is NOT loaded: appended at the end it would outrank
/// environment variables and the command line, which a developer's local file must never do.</para>
///
/// <para>OFF SWITCH. <c>DevelopmentLocalSettings:Enabled</c> — read from the sources already loaded when this runs,
/// environment variables (<c>DevelopmentLocalSettings__Enabled=false</c>) and command-line arguments included —
/// turns the file off. Absent means on; ANY value other than <c>true</c> means off. It exists for hosts that run
/// the real Program under Development from the real project directory but must not pick up whatever a developer
/// bootstrapped there (test hosts above all). It can only turn the file OFF: outside Development the file is never
/// loaded, whatever the switch says.</para>
///
/// <para>The same file exists in the Auth, Platform and MDM API projects (they share no project that reads JSON
/// configuration); a test keeps the three copies identical.</para>
/// </summary>
public static class DevelopmentLocalSettings
{
    public const string FileName = "appsettings.Development.local.json";
    public const string EnabledKey = "DevelopmentLocalSettings:Enabled";
    private const string TrackedDevelopmentFileName = "appsettings.Development.json";

    public static void AddDevelopmentLocalSettings(this IConfigurationBuilder configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        if (!environment.IsDevelopment()) return;
        if (IsSwitchedOff(configuration)) return;

        var tracked = configuration.Sources.OfType<JsonConfigurationSource>().LastOrDefault(source =>
            string.Equals(source.Path, TrackedDevelopmentFileName, StringComparison.OrdinalIgnoreCase));
        if (tracked is null) return;

        configuration.Sources.Insert(
            configuration.Sources.IndexOf(tracked) + 1,
            new JsonConfigurationSource { Path = FileName, Optional = true, ReloadOnChange = false });
    }

    private static bool IsSwitchedOff(IConfigurationBuilder configuration)
    {
        string? value;
        if (configuration is IConfiguration loaded)
        {
            // WebApplicationBuilder's ConfigurationManager: the sources added so far are already readable.
            value = loaded[EnabledKey];
        }
        else
        {
            var built = configuration.Build();
            try
            {
                value = built[EnabledKey];
            }
            finally
            {
                (built as IDisposable)?.Dispose();
            }
        }

        return value is not null && !(bool.TryParse(value, out var enabled) && enabled);
    }
}
