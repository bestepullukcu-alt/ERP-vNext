namespace Diten.Platform.Infrastructure.Settings;

public sealed class MdmServiceOptions
{
    public const string SectionName = "MdmService";

    public string BaseUrl { get; set; } = string.Empty;
}
