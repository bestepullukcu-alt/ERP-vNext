namespace Diten.Platform.Application.Contracts;

public interface ITenantDefaultsProvider
{
    string DefaultRegion { get; }
    string DefaultEnvironment { get; }
    string DefaultTier { get; }
    string DefaultLanguage { get; }
    string DefaultTimezone { get; }
    string DefaultCurrency { get; }
    string AppUrlTemplate { get; }
}
