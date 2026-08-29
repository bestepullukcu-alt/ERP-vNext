namespace Diten.PpmService.Persistence.Mongo;

public sealed class PpmMongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
}
