namespace Diten.BuildingBlocks.Security.Secrets;

public interface ISecretsProvider
{
    Task<string> GetSecretAsync(string key, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct);
}
