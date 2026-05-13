using Microsoft.IdentityModel.Tokens;

namespace Diten.BuildingBlocks.Security.Secrets;

public interface ISecretRotationResolver
{
    SecurityKey GetCurrentSigningKey();
    IReadOnlyList<SecurityKey> GetValidationKeys();
}
