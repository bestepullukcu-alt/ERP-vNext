namespace Diten.Platform.Application.Contracts.Eventing;

public sealed class EventBusOptions
{
    public const string SectionName = "Eventing";

    public string Producer { get; set; } = "Diten.Platform";

    public int MaxCanonicalPayloadBytes { get; set; } = 64 * 1024;
}
