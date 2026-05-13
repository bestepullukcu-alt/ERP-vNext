namespace Diten.BuildingBlocks.Security.Secrets;

public sealed class SecretsProviderOptions
{
    public const string SectionName = "SecretsProvider";

    public string ServiceName { get; set; } = string.Empty;
    public bool RequireEnvironmentVariablesInProduction { get; set; } = true;
}
