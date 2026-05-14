namespace Diten.BuildingBlocks.Security.Secrets;

public sealed class JwtSecretRotationOptions
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string[] PreviousSecrets { get; set; } = Array.Empty<string>();
}
