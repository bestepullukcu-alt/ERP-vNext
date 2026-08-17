namespace Diten.Platform.Infrastructure.Services.Notifications;

internal static class MessagingProviderErrorCodes
{
    public const string ProviderAuthFailed = "ProviderAuthFailed";
    public const string ProviderTlsFailed = "ProviderTlsFailed";
    public const string ProviderTimeout = "ProviderTimeout";
    public const string ProviderConnectivityFailed = "ProviderConnectivityFailed";
    public const string ProviderRejected = "ProviderRejected";
    public const string ProviderRejectedRecipientLimit = "ProviderRejected:RecipientLimit";
    public const string ProviderConfigInvalid = "ProviderConfigInvalid";
    public const string ProviderSecretUnresolved = "ProviderSecretUnresolved";
    public const string ProviderUnknown = "ProviderUnknown";
}
