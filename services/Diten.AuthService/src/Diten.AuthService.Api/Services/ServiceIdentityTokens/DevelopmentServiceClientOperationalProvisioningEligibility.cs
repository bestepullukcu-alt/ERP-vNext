using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

public sealed class DevelopmentServiceClientOperationalProvisioningEligibility
    : IServiceClientOperationalProvisioningEligibility
{
    private readonly string _environment;
    private readonly ServiceClientOperationalProvisioningOptions _options;

    public DevelopmentServiceClientOperationalProvisioningEligibility(IHostEnvironment environment,
        IOptions<ServiceClientOperationalProvisioningOptions> options)
    {
        _environment = environment.EnvironmentName;
        _options = options.Value.Snapshot();
    }

    public void EnsureEligible(ServiceClientOperationalProvisioningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(_environment, "Development", StringComparison.Ordinal) || !_options.Enabled)
            throw Invalid();
        if (_options.MaximumInputBytes is < 1024 or > 64 * 1024
            || _options.MaximumOperatorTokenAgeSeconds is < 1 or > 300
            || _options.OperationTimeoutSeconds is < 1 or > 120
            || _options.OperationalMarkerSha256.Length != 64
            || !_options.OperationalMarkerSha256.All(Uri.IsHexDigit)
            || !ServiceClientOperationalOperations.IsSupported(_options.AuthorizedOperation)
            || _options.AuthorizedCommandId == Guid.Empty
            || _options.AuthorizedExpectedOperationalVersion is < 0 or long.MaxValue)
            throw Invalid();
        if (_options.AuthorizedServiceClientIdentityId is null
            || _options.AuthorizedServiceClientIdentityId == Guid.Empty
            || !ValidText(_options.AuthorizedClientCode) || !ValidText(_options.AuthorizedServiceName)
            || !ValidText(_options.AuthorizedAudience)) throw Invalid();

        if (!Same(request.Operation, _options.AuthorizedOperation)
            || request.CommandId != _options.AuthorizedCommandId
            || request.ExpectedOperationalVersion != _options.AuthorizedExpectedOperationalVersion
            || request.ServiceClientIdentityId != _options.AuthorizedServiceClientIdentityId
            || request.TenantId != _options.AuthorizedTenantId
            || !Same(request.ClientCode, _options.AuthorizedClientCode)
            || !Same(request.ServiceName, _options.AuthorizedServiceName)
            || !Same(request.Audience, _options.AuthorizedAudience)
            || !Same(request.ExpectedCredentialVersion, _options.AuthorizedExpectedCredentialVersion)) throw Invalid();

        if (request.Operation == ServiceClientOperationalOperations.ReadGrant)
        {
            if (request.TenantId is null || request.TenantId == Guid.Empty) throw Invalid();
        }
        else if (request.TenantId is not null) throw Invalid();

        if (request.Operation == ServiceClientOperationalOperations.RotateCredential)
        {
            if (!ValidText(request.ExpectedCredentialVersion) || !ValidText(request.InheritedPipeHandle)) throw Invalid();
        }
        else if (request.InheritedPipeHandle is not null || request.ExpectedCredentialVersion is not null)
            throw Invalid();
    }

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.Ordinal);
    private static bool ValidText(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128
        && Same(value, value.Trim()) && !value.Any(char.IsControl);
    private static ServiceClientOperationalContractException Invalid() =>
        new("The process-authorized operational envelope is invalid or does not match.");
}
