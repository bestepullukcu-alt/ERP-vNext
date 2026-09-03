using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

public sealed class DevelopmentServiceClientOperationalProvisioningEligibility
    : IServiceClientOperationalProvisioningEligibility
{
    private readonly IHostEnvironment _environment;
    private readonly ServiceClientOperationalProvisioningOptions _options;

    public DevelopmentServiceClientOperationalProvisioningEligibility(
        IHostEnvironment environment,
        IOptions<ServiceClientOperationalProvisioningOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    public void EnsureEligible(ServiceClientOperationalProvisioningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_environment.IsDevelopment() || !_options.Enabled)
        {
            throw new ServiceClientOperationalContractException(
                "Service-client operational provisioning is disabled for this environment.");
        }

        if (_options.MaximumInputBytes is < 1024 or > 64 * 1024
            || _options.MaximumOperatorTokenAgeSeconds is < 1 or > 300
            || _options.OperationTimeoutSeconds is < 1 or > 120
            || _options.OperationalMarkerSha256.Length != 64
            || !IsSha256Hex(_options.OperationalMarkerSha256)
            || !ServiceClientOperationalOperations.IsSupported(_options.AuthorizedOperation)
            || _options.AuthorizedCommandId == Guid.Empty
            || _options.AuthorizedExpectedOperationalVersion < 0
            || string.IsNullOrEmpty(_options.AuthorizedClientCode)
            || string.IsNullOrEmpty(_options.AuthorizedServiceName)
            || string.IsNullOrEmpty(_options.AuthorizedAudience))
        {
            throw new ServiceClientOperationalContractException(
                "Service-client operational provisioning eligibility facts are invalid.");
        }

        if (!string.Equals(request.Operation, _options.AuthorizedOperation, StringComparison.Ordinal)
            || request.CommandId != _options.AuthorizedCommandId
            || request.ExpectedOperationalVersion != _options.AuthorizedExpectedOperationalVersion
            || request.ServiceClientIdentityId != _options.AuthorizedServiceClientIdentityId
            || request.TenantId != _options.AuthorizedTenantId
            || !string.Equals(request.ClientCode, _options.AuthorizedClientCode, StringComparison.Ordinal)
            || !string.Equals(request.ServiceName, _options.AuthorizedServiceName, StringComparison.Ordinal)
            || !string.Equals(request.Audience, _options.AuthorizedAudience, StringComparison.Ordinal))
        {
            throw new ServiceClientOperationalContractException(
                "The request does not match the authorized immutable operational facts.");
        }

        if (ServiceClientOperationalOperations.EmitsSecret(request.Operation)
            && string.IsNullOrWhiteSpace(request.InheritedPipeHandle))
        {
            throw new ServiceClientOperationalContractException(
                "A protected inherited one-shot pipe is required for this operation.");
        }

        if (!ServiceClientOperationalOperations.EmitsSecret(request.Operation)
            && request.InheritedPipeHandle is not null)
        {
            throw new ServiceClientOperationalContractException(
                "A secret pipe is forbidden for operations that do not issue a credential.");
        }
    }

    private static bool IsSha256Hex(string value)
        => value.All(character => character is >= '0' and <= '9'
                                  or >= 'a' and <= 'f'
                                  or >= 'A' and <= 'F');
}
