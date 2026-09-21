namespace Diten.PpmService.Infrastructure.Portfolios;

// The environment owner supplies this profile separately; no runtime endpoint or credential is provisioned here.
public sealed class PortfolioAuthorityOptions
{
    public const string SectionName = "PortfolioOwnerAuthority";
    public bool Enabled { get; init; }
    public int TimeoutSeconds { get; init; } = 10;
    public string? NonProductionEnvironmentName { get; init; }
    public string? ApprovedAuthOrigin { get; init; }
    public string? TrustProfileOwner { get; init; }
    public string? TrustProfileApprovalReference { get; init; }

    public bool IsValid => Enabled && TimeoutSeconds is > 0 and <= 30 &&
        PortfolioAuthTrustedTarget.IsConfigurationValid(this);
}
