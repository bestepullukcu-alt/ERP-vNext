using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class RefreshTokenHasher : IRefreshTokenHasher
{
    private readonly JwtSettings _jwtSettings;

    public RefreshTokenHasher(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string Hash(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));
        }

        var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);
        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        return Convert.ToBase64String(HMACSHA256.HashData(key, bytes));
    }
}
