using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Settings;

public sealed class SmtpProviderOptionsValidator : IValidateOptions<SmtpProviderOptions>
{
    private readonly IHostEnvironment _environment;

    public SmtpProviderOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, SmtpProviderOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail($"{SmtpProviderOptions.SectionName} configuration section is missing.");
        }

        var failures = new List<string>();

        if (options.SendTimeoutSeconds <= 0 || options.SendTimeoutSeconds > 300)
        {
            failures.Add($"{SmtpProviderOptions.SectionName}:SendTimeoutSeconds must be between 1 and 300 (was {options.SendTimeoutSeconds}).");
        }

        if (options.MaxRecipientsPerMessage <= 0 || options.MaxRecipientsPerMessage > 1000)
        {
            failures.Add($"{SmtpProviderOptions.SectionName}:MaxRecipientsPerMessage must be between 1 and 1000 (was {options.MaxRecipientsPerMessage}).");
        }

        if (options.AllowInsecureTlsInDevelopment && _environment.IsProduction())
        {
            failures.Add($"{SmtpProviderOptions.SectionName}:AllowInsecureTlsInDevelopment must be false in the Production environment.");
        }

        if (options.SubjectLogPreviewLength < 0 || options.SubjectLogPreviewLength > 256)
        {
            failures.Add($"{SmtpProviderOptions.SectionName}:SubjectLogPreviewLength must be between 0 and 256 (was {options.SubjectLogPreviewLength}).");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
