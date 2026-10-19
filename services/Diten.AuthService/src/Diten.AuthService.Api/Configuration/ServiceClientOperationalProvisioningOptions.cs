namespace Diten.AuthService.Api.Configuration;

/// <summary>Bound only by the isolated, process-environment configuration provider.</summary>
public sealed class ServiceClientOperationalProvisioningOptions
{
    public const string SectionName = "ServiceClientOperationalProvisioning";
    public bool Enabled { get; set; }
    public string OperationalMarkerSha256 { get; set; } = string.Empty;
    public int MaximumInputBytes { get; set; } = 64 * 1024;
    public int MaximumOperatorTokenAgeSeconds { get; set; } = 300;
    public int OperationTimeoutSeconds { get; set; } = 30;
    public string AuthorizedOperation { get; set; } = string.Empty;
    public Guid AuthorizedCommandId { get; set; }
    public long AuthorizedExpectedOperationalVersion { get; set; } = -1;
    public Guid? AuthorizedServiceClientIdentityId { get; set; }
    public Guid? AuthorizedTenantId { get; set; }
    public string AuthorizedClientCode { get; set; } = string.Empty;
    public string AuthorizedServiceName { get; set; } = string.Empty;
    public string AuthorizedAudience { get; set; } = string.Empty;
    public string? AuthorizedExpectedCredentialVersion { get; set; }

    // Copies scalar/string values; later mutation of a bound options object cannot widen an invocation.
    internal ServiceClientOperationalProvisioningOptions Snapshot() => new()
    {
        Enabled = Enabled,
        OperationalMarkerSha256 = OperationalMarkerSha256,
        MaximumInputBytes = MaximumInputBytes,
        MaximumOperatorTokenAgeSeconds = MaximumOperatorTokenAgeSeconds,
        OperationTimeoutSeconds = OperationTimeoutSeconds,
        AuthorizedOperation = AuthorizedOperation,
        AuthorizedCommandId = AuthorizedCommandId,
        AuthorizedExpectedOperationalVersion = AuthorizedExpectedOperationalVersion,
        AuthorizedServiceClientIdentityId = AuthorizedServiceClientIdentityId,
        AuthorizedTenantId = AuthorizedTenantId,
        AuthorizedClientCode = AuthorizedClientCode,
        AuthorizedServiceName = AuthorizedServiceName,
        AuthorizedAudience = AuthorizedAudience,
        AuthorizedExpectedCredentialVersion = AuthorizedExpectedCredentialVersion
    };
}
