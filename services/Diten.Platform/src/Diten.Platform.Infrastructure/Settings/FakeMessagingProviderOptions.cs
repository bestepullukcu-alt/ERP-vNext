namespace Diten.Platform.Infrastructure.Settings;

public sealed class FakeMessagingProviderOptions
{
    public const string SectionName = "Notifications:FakeProvider";
    public bool Enabled { get; set; }
}
