namespace Diten.PpmService.Infrastructure.Portfolios;

// Deliberately disabled by default. Runtime composition has no Auth endpoint in this delivery;
// only the isolated acceptance host enables the adapter.
public sealed class PortfolioAuthorityOptions
{
    public const string SectionName = "PortfolioOwnerAuthority";
    public bool Enabled { get; init; }
    public int TimeoutSeconds { get; init; } = 10;

    public bool IsValid => Enabled && TimeoutSeconds is > 0 and <= 30;
}
