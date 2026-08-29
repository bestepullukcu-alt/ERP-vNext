using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Features.Workflow.Services;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Security;

public sealed class ConfiguredTrustedWorkflowStartAuthorizationPolicy :
    ITrustedWorkflowStartAuthorizationPolicy
{
    public const string ConfigurationError =
        "WORKFLOW_TRUSTED_START_AUTHORIZATION_CONFIGURATION_INVALID";

    private readonly IReadOnlyList<TrustedWorkflowStartAuthorizationEntry> _entries;

    public ConfiguredTrustedWorkflowStartAuthorizationPolicy(
        IOptions<TrustedWorkflowStartAuthorizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value ?? new TrustedWorkflowStartAuthorizationOptions();
        var validation = TrustedWorkflowStartAuthorizationOptionsValidator.ValidateValue(value);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                Options.DefaultName,
                typeof(TrustedWorkflowStartAuthorizationOptions),
                validation.Failures);
        }

        _entries = value.Entries.ToArray();
    }

    public bool IsAuthorized(TrustedWorkflowStartAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _entries.Any(entry =>
            entry.ClientId == request.ClientId
            && string.Equals(entry.ServiceName, request.ServiceName, StringComparison.Ordinal)
            && string.Equals(entry.Audience, request.Audience, StringComparison.Ordinal)
            && string.Equals(entry.ObjectType, request.ObjectType, StringComparison.Ordinal)
            && entry.TemplateId == request.TemplateId
            && string.Equals(entry.TemplateCode, request.TemplateCode, StringComparison.Ordinal));
    }

}

public sealed class TrustedWorkflowStartAuthorizationOptionsValidator :
    IValidateOptions<TrustedWorkflowStartAuthorizationOptions>
{
    public ValidateOptionsResult Validate(string? name, TrustedWorkflowStartAuthorizationOptions options) =>
        ValidateValue(options);

    internal static ValidateOptionsResult ValidateValue(
        TrustedWorkflowStartAuthorizationOptions options)
    {
        if (options is null
            || options.Entries is null
            || options.Entries.Count > TrustedWorkflowStartAuthorizationOptions.MaximumEntries)
        {
            return ValidateOptionsResult.Fail(
                ConfiguredTrustedWorkflowStartAuthorizationPolicy.ConfigurationError);
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in options.Entries)
        {
            if (!IsValid(entry) || !unique.Add(Key(entry)))
            {
                return ValidateOptionsResult.Fail(
                    ConfiguredTrustedWorkflowStartAuthorizationPolicy.ConfigurationError);
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValid(TrustedWorkflowStartAuthorizationEntry? entry) =>
        entry is not null
        && entry.ClientId != Guid.Empty
        && string.Equals(entry.ServiceName, "Diten.MDM", StringComparison.Ordinal)
        && string.Equals(entry.Audience, "TRUSTED_WORKFLOW_CONSUMER", StringComparison.Ordinal)
        && IsExact(entry.ObjectType, 128)
        && (entry.TemplateId.HasValue ^ entry.TemplateCode is not null)
        && (!entry.TemplateId.HasValue || entry.TemplateId.Value != Guid.Empty)
        && (entry.TemplateCode is null || IsExact(entry.TemplateCode, 128));

    private static bool IsExact(string? value, int maximumLength) =>
        value is { Length: > 0 }
        && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(character => char.IsControl(character) || character is '*' or '?');

    private static string Key(TrustedWorkflowStartAuthorizationEntry entry) =>
        string.Join("\u001f",
            entry.ClientId.ToString("D"),
            entry.ServiceName,
            entry.Audience,
            entry.ObjectType,
            entry.TemplateId?.ToString("D") ?? string.Empty,
            entry.TemplateCode ?? string.Empty);
}
