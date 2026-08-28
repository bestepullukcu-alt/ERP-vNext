using Diten.Platform.Application.Features.WorkingCalendarImport;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Settings;

public sealed class WorkingCalendarImportOptionsValidator : IValidateOptions<WorkingCalendarImportOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkingCalendarImportOptions options)
    {
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return ValidateOptionsResult.Fail("WorkingCalendar holiday provider BaseUrl must be an absolute HTTPS URL.");
        if (options.AllowedHosts.Count == 0 || !options.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail("WorkingCalendar holiday provider host must be present in AllowedHosts.");
        if (options.TimeoutSeconds is < 1 or > 30 || options.MaxResponseItems is < 1 or > 2000)
            return ValidateOptionsResult.Fail("WorkingCalendar holiday provider limits are invalid.");
        if (!string.Equals(options.Provider, "offline-stub", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.Provider, "nager-date", StringComparison.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail("WorkingCalendar holiday provider is not supported.");
        return ValidateOptionsResult.Success;
    }
}
