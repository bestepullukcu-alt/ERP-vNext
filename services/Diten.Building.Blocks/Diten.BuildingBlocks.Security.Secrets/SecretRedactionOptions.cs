namespace Diten.BuildingBlocks.Security.Secrets;

public sealed class SecretRedactionOptions
{
    public string Mask { get; set; } = "***REDACTED***";
    public string[] SensitiveKeyTokens { get; set; } =
    [
        "Secret",
        "ApiKey",
        "Password",
        "HashSecret",
        "Token",
        "ConnectionString"
    ];
}
