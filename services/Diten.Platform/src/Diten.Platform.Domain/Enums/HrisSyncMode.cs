namespace Diten.Platform.Domain.Enums;

public enum HrisSyncMode
{
    Manual = 1,
    ScheduledPull = 2,
    Webhook = 3,
    FileImport = 4,
    Disabled = 5
}
