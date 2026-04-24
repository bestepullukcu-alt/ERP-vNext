using Microsoft.AspNetCore.Http;

namespace Diten.Web.Services.Auth;

public interface IAuthCookieService
{
    void WriteTokens(HttpResponse response, string accessToken, string refreshToken, DateTime refreshExpiresAtUtc);
    void ClearTokens(HttpResponse response);
}
