using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.Features.ExternalContextReferences;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Authorization;


public sealed class ExternalContextProviderOptionsValidator
    : IValidateOptions<ExternalContextProviderOptions>
{
    private static readonly string[] ForbiddenFragments =
        ["changeme", "placeholder", "default", "example", "development", "secret"];

    public ValidateOptionsResult Validate(string? name, ExternalContextProviderOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var credential = options.ServiceCredential;
        if (string.IsNullOrWhiteSpace(credential) || credential.Length < 24 ||
            ForbiddenFragments.Any(fragment => credential.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            return ValidateOptionsResult.Fail(
                "ExternalContextProvider requires a dedicated non-placeholder credential of at least 24 characters when enabled.");
        }

        if (options.LookupTimeoutMilliseconds is < 100 or > 5000)
        {
            return ValidateOptionsResult.Fail(
                "ExternalContextProvider lookup timeout must be between 100 and 5000 milliseconds when enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
