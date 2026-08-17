namespace Diten.AuthService.Application.Common.Interfaces;

public interface IRefreshTokenHasher
{
    string Hash(string refreshToken);
}
