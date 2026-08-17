using Diten.BuildingBlocks.Security.Secrets;

namespace Diten.Platform.Infrastructure.Services.Notifications;

public sealed record SecretResolutionResult(bool IsSuccessful, string? Value, string? ErrorCode, string? ErrorMessage)
{
    public static SecretResolutionResult Success(string value) => new(true, value, null, null);

    public static SecretResolutionResult Fail(string errorCode, string errorMessage) =>
        new(false, null, errorCode, errorMessage);
}

public sealed class SecretReferenceResolver
{
    private readonly ISecretsProvider _secretsProvider;

    public SecretReferenceResolver(ISecretsProvider secretsProvider)
    {
        _secretsProvider = secretsProvider;
    }

    public async Task<SecretResolutionResult> ResolveAsync(string? credentialSecretRef, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credentialSecretRef))
        {
            return SecretResolutionResult.Fail(
                MessagingProviderErrorCodes.ProviderConfigInvalid,
                "Credential reference is missing.");
        }

        string secretValue;
        try
        {
            secretValue = await _secretsProvider.GetSecretAsync(credentialSecretRef, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SecretResolutionResult.Fail(
                MessagingProviderErrorCodes.ProviderSecretUnresolved,
                "Secret could not be resolved.");
        }

        if (string.IsNullOrEmpty(secretValue))
        {
            return SecretResolutionResult.Fail(
                MessagingProviderErrorCodes.ProviderSecretUnresolved,
                "Secret could not be resolved.");
        }

        return SecretResolutionResult.Success(secretValue);
    }
}
