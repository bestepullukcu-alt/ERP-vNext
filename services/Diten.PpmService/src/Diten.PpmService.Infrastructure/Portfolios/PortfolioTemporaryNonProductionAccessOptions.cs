namespace Diten.PpmService.Infrastructure.Portfolios;

// Deliberately opt-in: an absent section, false value, or unmatched host name remains unavailable.
public sealed class PortfolioTemporaryNonProductionAccessOptions
{
    public const string SectionName = "PortfolioTemporaryNonProductionAccess";

    public bool Enabled { get; init; }
    public string? NonProductionEnvironmentName { get; init; }
}
