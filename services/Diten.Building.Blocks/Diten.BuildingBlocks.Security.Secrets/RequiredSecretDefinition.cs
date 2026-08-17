namespace Diten.BuildingBlocks.Security.Secrets;

public sealed record RequiredSecretDefinition(
    string Key,
    string ServiceContext,
    SecretRequirementKind Kind = SecretRequirementKind.Generic,
    bool Required = true,
    Func<bool>? IsEnabled = null,
    int? MinimumLength = null);

public enum SecretRequirementKind
{
    Generic,
    JwtCurrent,
    JwtPreviousCollection,
    InternalApiKey,
    ConnectionString
}
