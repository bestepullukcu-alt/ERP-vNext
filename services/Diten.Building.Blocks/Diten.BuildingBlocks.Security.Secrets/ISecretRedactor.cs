namespace Diten.BuildingBlocks.Security.Secrets;

public interface ISecretRedactor
{
    string Redact(string key, string? value);
}
