using Diten.Platform.Application.Contracts.Audit;

namespace Diten.Platform.Application.Features.Audit;

public sealed class SensitiveFieldRedactionRegistry : ISensitiveFieldRedactionRegistry
{
    private static readonly IReadOnlyList<SensitiveFieldRule> Rules =
    [
        new("password"),
        new("passwordHash"),
        new("pwd"),
        new("token"),
        new("accessToken"),
        new("refreshToken"),
        new("secret"),
        new("clientSecret"),
        new("apiSecret"),
        new("apiKey"),
        new("connectionString"),
        new("authorization"),
        new("authorizationHeader"),
        new("privateKey"),
        new("headers.authorization"),
        new("metadata.authorization"),
        new("credentials.password"),
        new("credentials.apiKey")
    ];

    public IReadOnlyList<SensitiveFieldRule> GetRules()
    {
        return Rules;
    }
}
