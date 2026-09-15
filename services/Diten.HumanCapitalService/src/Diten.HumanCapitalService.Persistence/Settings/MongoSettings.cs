namespace Diten.HumanCapitalService.Persistence.Settings;

public sealed class MongoSettings
{
    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
}
