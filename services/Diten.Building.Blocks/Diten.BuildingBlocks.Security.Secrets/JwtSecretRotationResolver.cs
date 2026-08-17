using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Diten.BuildingBlocks.Security.Secrets;

public sealed class JwtSecretRotationResolver : ISecretRotationResolver
{
    private readonly IConfiguration _configuration;

    public JwtSecretRotationResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SecurityKey GetCurrentSigningKey()
    {
        var current = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrWhiteSpace(current))
        {
            throw new SecretValidationException("JwtSettings", ["'JwtSettings:Secret' is required."]);
        }

        return CreateKey(current);
    }

    public IReadOnlyList<SecurityKey> GetValidationKeys()
    {
        var secrets = new List<string>();
        var current = _configuration["JwtSettings:Secret"];
        if (!string.IsNullOrWhiteSpace(current))
        {
            secrets.Add(current);
        }

        secrets.AddRange(_configuration.GetSection("JwtSettings:PreviousSecrets")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))!);

        return secrets.Select(CreateKey).ToArray();
    }

    private static SecurityKey CreateKey(string secret) =>
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
}
