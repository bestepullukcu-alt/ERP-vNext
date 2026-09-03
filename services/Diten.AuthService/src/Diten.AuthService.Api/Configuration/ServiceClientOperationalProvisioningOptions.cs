namespace Diten.AuthService.Api.Configuration;

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
}
