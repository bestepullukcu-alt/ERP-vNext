using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.PlatformAdministrators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.Platform;

[Authorize]
[Route("Platform/Administrators")]
public sealed class AdministratorsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<AdministratorsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public AdministratorsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<AdministratorsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/Administrators/Index.cshtml");

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create([FromForm] PlatformAdministratorEditViewModel model) =>
        SaveFormAsync(null, model);

    [HttpPost("edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(Guid id, [FromForm] PlatformAdministratorEditViewModel model) =>
        SaveFormAsync(id, model);

    [HttpGet("get/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!TryCreateAuthenticatedRequest(HttpMethod.Get, $"{_gatewayUrl}/api/platform/administrators/{id}", out var request))
        {
            Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode(StatusCodes.Status401Unauthorized, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
        }

        try
        {
            using (request)
            {
                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false });
                }

                var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<PlatformAdministratorDetailViewModel>>(_jsonOptions);
                return payload?.Data is null
                    ? Json(new { success = false })
                    : Json(new { success = true, data = payload.Data });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform administrator get-by-id failed for {AdministratorId}.", id);
            return Json(new { success = false });
        }
    }

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        var targetUrl = $"{_gatewayUrl}/api/platform/administrators{Request.QueryString}";
        return ProxyGatewayAsync(HttpMethod.Get, targetUrl);
    }

    [HttpGet("api/stats")]
    public Task<IActionResult> StatsProxy() =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/administrators/stats");

    [HttpDelete("api/{id:guid}")]
    public async Task<IActionResult> DeleteProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/platform/administrators/{id}", body);
    }

    [HttpDelete("api/bulk")]
    public async Task<IActionResult> BulkDeleteProxy()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/platform/administrators/bulk", body);
    }

    [HttpPost("api/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/administrators/{id}/suspend", body);
    }

    [HttpPost("api/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/administrators/{id}/reactivate", body);
    }

    [HttpPost("api/{id:guid}/roles")]
    public async Task<IActionResult> AssignRolesProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/administrators/{id}/roles", body);
    }

    [HttpPost("api/{id:guid}/resend-invite")]
    public async Task<IActionResult> ResendInviteProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/administrators/{id}/resend-invite", body);
    }

    [HttpPost("api/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPasswordProxy(Guid id, [FromForm] string email)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_gatewayUrl}/api/platform-auth/forgot-password")
            {
                Content = JsonContent.Create(new { email }, options: _jsonOptions)
            };
            AddAuthHeader(request);
            using var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            return Json(new { success = false, errors = new[] { "Failed to send reset link." } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger password reset email for {Email}.", email);
            return Json(new { success = false, errors = new[] { _sharedLocalizer["GatewayError"].Value } });
        }
    }

    private async Task<IActionResult> SaveFormAsync(Guid? id, PlatformAdministratorEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new
            {
                success = false,
                errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        try
        {
            var payload = ToPayload(model);
            var method = id.HasValue ? HttpMethod.Put : HttpMethod.Post;
            var targetUrl = id.HasValue
                ? $"{_gatewayUrl}/api/platform/administrators/{id}"
                : $"{_gatewayUrl}/api/platform/administrators";
            if (!TryCreateAuthenticatedRequest(method, targetUrl, out var request))
            {
                Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
                return StatusCode(StatusCodes.Status401Unauthorized, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
            }

            using (request)
            {
                request.Content = JsonContent.Create(payload, options: _jsonOptions);
                using var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    PlatformAdministratorInviteResultViewModel? inviteResult = null;
                    if (!id.HasValue)
                    {
                        var invitePayload = await response.Content.ReadFromJsonAsync<GatewayResponse<PlatformAdministratorInviteResultViewModel>>(_jsonOptions);
                        inviteResult = invitePayload?.Data;
                    }

                    return Json(new
                    {
                        success = true,
                        devSetupUrl = inviteResult?.SetupUrl,
                        emailSent = inviteResult?.EmailSent
                    });
                }

                return Json(new { success = false, errors = await ExtractGatewayErrorsAsync(response) });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform administrator save failed for {AdministratorId}.", id);
            return Json(new { success = false, errors = new[] { _sharedLocalizer["GatewayError"].Value } });
        }
    }

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, string? jsonBody = null)
    {
        if (!TryCreateAuthenticatedRequest(method, targetUrl, out var request))
        {
            Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
            return StatusCode(StatusCodes.Status401Unauthorized, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
        }

        using (request)
        {
            if (jsonBody is not null)
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            using var response = await _httpClient.SendAsync(request);
            if (Diten.Web.Controllers.ProxyAuthFailure.IsAuthFailure(response.StatusCode))
            {
                Diten.Web.Controllers.ProxyAuthFailure.ClearAuthCookies(Response);
                return StatusCode((int)response.StatusCode, Diten.Web.Controllers.ProxyAuthFailure.PlatformLoginPayload());
            }

            var content = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            return new ContentResult
            {
                Content = content,
                ContentType = contentType,
                StatusCode = (int)response.StatusCode
            };
        }
    }

    private bool TryCreateAuthenticatedRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        if (TryGetPlatformAccessToken(out var token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return true;
        }

        request.Dispose();
        request = null!;
        return false;
    }

    private void AddAuthHeader(HttpRequestMessage request)
    {
        if (TryGetPlatformAccessToken(out var token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private bool TryGetPlatformAccessToken(out string token)
    {
        token = Request.Cookies["access_token"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var actorType = FindClaim(jwt.Claims, "actor_type");
            return jwt.ValidTo > DateTime.UtcNow &&
                   (string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            token = string.Empty;
            return false;
        }
    }

    private static string? FindClaim(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = claims.FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [_sharedLocalizer["GatewayError"].Value];
        }

        var isTurkish = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // 1. Try to parse "errors" property (can be list or dictionary)
            if (root.TryGetProperty("errors", out var errorsProp) || root.TryGetProperty("Errors", out errorsProp))
            {
                if (errorsProp.ValueKind == JsonValueKind.Array)
                {
                    var errors = new List<string>();
                    foreach (var item in errorsProp.EnumerateArray())
                    {
                        var str = item.GetString();
                        if (!string.IsNullOrWhiteSpace(str)) errors.Add(LocalizeErrorMessage(str, isTurkish));
                    }
                    if (errors.Count > 0) return errors;
                }
                else if (errorsProp.ValueKind == JsonValueKind.Object)
                {
                    var errors = new List<string>();
                    foreach (var prop in errorsProp.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in prop.Value.EnumerateArray())
                            {
                                var str = item.GetString();
                                if (!string.IsNullOrWhiteSpace(str)) errors.Add(LocalizeErrorMessage(str, isTurkish));
                            }
                        }
                    }
                    if (errors.Count > 0) return errors;
                }
            }

            // 2. Try to parse standard "detail" property (standard RFC ProblemDetails / Custom Exceptions)
            if (root.TryGetProperty("detail", out var detailProp) || root.TryGetProperty("Detail", out detailProp))
            {
                var detail = detailProp.GetString();
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    if (detail.StartsWith("Validation failed:", StringComparison.OrdinalIgnoreCase))
                    {
                        detail = detail.Replace("Validation failed:", "").Trim();
                        detail = detail.Replace("\r", "").Replace("\n", " ").Replace(" --", " ").Replace("Severity: Error", "").Trim();
                        var colonIndex = detail.IndexOf(':');
                        if (colonIndex >= 0 && colonIndex < detail.Length - 1)
                        {
                            detail = detail[(colonIndex + 1)..].Trim();
                        }
                    }
                    return [LocalizeErrorMessage(detail, isTurkish)];
                }
            }

            // 3. Try to parse "message" property (standard Response envelope)
            if (root.TryGetProperty("message", out var messageProp) || root.TryGetProperty("Message", out messageProp))
            {
                var message = messageProp.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    if (message.StartsWith("Validation failed:", StringComparison.OrdinalIgnoreCase))
                    {
                        message = message.Replace("Validation failed:", "").Trim();
                        message = message.Replace("\r", "").Replace("\n", " ").Replace(" --", " ").Replace("Severity: Error", "").Trim();
                        var colonIndex = message.IndexOf(':');
                        if (colonIndex >= 0 && colonIndex < message.Length - 1)
                        {
                            message = message[(colonIndex + 1)..].Trim();
                        }
                    }
                    return [LocalizeErrorMessage(message, isTurkish)];
                }
            }
        }
        catch { }

        return [LocalizeErrorMessage(raw, isTurkish)];
    }

    private string LocalizeErrorMessage(string message, bool isTurkish)
    {
        if (string.IsNullOrWhiteSpace(message)) return message;

        var normalized = message.Trim();
        if (normalized.Contains("You cannot perform this action on your own account", StringComparison.OrdinalIgnoreCase))
        {
            return _sharedLocalizer["AdminSelfActionDenied"].Value;
        }

        if (normalized.Contains("You cannot remove your own administrative role", StringComparison.OrdinalIgnoreCase))
        {
            return _sharedLocalizer["AdminSelfRoleDowngradeDenied"].Value;
        }

        if (normalized.Contains("At least one active Super Admin must remain in the system", StringComparison.OrdinalIgnoreCase))
        {
            return _sharedLocalizer["AdminLastSuperAdminDenied"].Value;
        }

        if (normalized.Contains("The system seed administrator cannot be deleted", StringComparison.OrdinalIgnoreCase))
        {
            return _sharedLocalizer["AdminSeedDeleteDenied"].Value;
        }

        if (normalized.Contains("The system seed administrator cannot be suspended", StringComparison.OrdinalIgnoreCase))
        {
            return _sharedLocalizer["AdminSeedSuspendDenied"].Value;
        }

        if (isTurkish)
        {
            // Remove "Password: " or similar prefix noise
            var clean = message.Replace("Password:", "").Replace("Password :", "").Trim();

            // Check if there are multiple rules joined together
            var parts = clean.Split(new[] { '.', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var translatedParts = new List<string>();

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;

                if (part.Contains("must contain at least one uppercase letter", StringComparison.OrdinalIgnoreCase))
                {
                    translatedParts.Add("Şifre en az bir büyük harf içermelidir.");
                }
                else if (part.Contains("must contain at least one special character", StringComparison.OrdinalIgnoreCase))
                {
                    translatedParts.Add("Şifre en az bir özel karakter içermelidir.");
                }
                else if (part.Contains("must be at least", StringComparison.OrdinalIgnoreCase) && part.Contains("characters", StringComparison.OrdinalIgnoreCase))
                {
                    var digits = new string(part.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrEmpty(digits))
                    {
                        if (part.Contains("Temporary password", StringComparison.OrdinalIgnoreCase))
                        {
                            translatedParts.Add($"Geçici şifre en az {digits} karakter olmalıdır.");
                        }
                        else
                        {
                            translatedParts.Add($"Şifre en az {digits} karakter olmalıdır.");
                        }
                    }
                    else
                    {
                        translatedParts.Add(part);
                    }
                }
                else if (part.Contains("Temporary password is required", StringComparison.OrdinalIgnoreCase))
                {
                    translatedParts.Add("Geçici şifre zorunludur ve en az 10 karakter olmalıdır.");
                }
                else if (part.Contains("must not exceed", StringComparison.OrdinalIgnoreCase) && part.Contains("characters", StringComparison.OrdinalIgnoreCase))
                {
                    var digits = new string(part.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrEmpty(digits))
                    {
                        if (part.Contains("Temporary password", StringComparison.OrdinalIgnoreCase))
                        {
                            translatedParts.Add($"Geçici şifre {digits} karakteri aşmamalıdır.");
                        }
                        else
                        {
                            translatedParts.Add($"Şifre {digits} karakteri aşmamalıdır.");
                        }
                    }
                    else
                    {
                        translatedParts.Add(part);
                    }
                }
                else if (part.Contains("Password is required", StringComparison.OrdinalIgnoreCase))
                {
                    translatedParts.Add("Şifre zorunludur.");
                }
                else if (part.Contains("Administrator email already exists", StringComparison.OrdinalIgnoreCase))
                {
                    translatedParts.Add("Yönetici e-posta adresi zaten mevcut.");
                }
                else if (part.Contains("Administrator username already exists", StringComparison.OrdinalIgnoreCase) ||
                         part.Contains("username already exists", StringComparison.OrdinalIgnoreCase))
                {
                    translatedParts.Add("Yönetici kullanıcı adı zaten mevcut.");
                }
                else if (part.Contains("Administrator was created, but the invitation email could not be sent", StringComparison.OrdinalIgnoreCase))
                {
                    translatedParts.Add("Yönetici oluşturuldu ancak davet e-postası gönderilemedi. SMTP ayarlarını kontrol edip Daveti Yeniden Gönder işlemini kullanın.");
                }
                else if (part.Contains("Invitation state was updated, but the invitation email could not be sent", StringComparison.OrdinalIgnoreCase))
                {
                    translatedParts.Add("Davet durumu güncellendi ancak davet e-postası gönderilemedi. SMTP ayarlarını kontrol edip tekrar deneyin.");
                }
                else
                {
                    translatedParts.Add(part);
                }
            }

            if (translatedParts.Count > 0)
            {
                return string.Join(" ", translatedParts);
            }
        }

        return message;
    }

    private static PlatformAdministratorSavePayload ToPayload(PlatformAdministratorEditViewModel model) => new()
    {
        Email = model.Email,
        UserName = model.UserName,
        DisplayName = model.DisplayName,
        ActorType = model.ActorType,
        PartnerId = model.PartnerId,
        AllowedTenantIds = ParseTenantIds(model.AllowedTenantIdsRaw),
        Roles = model.Roles?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList() ?? [],
        Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status,
        Version = model.Version,
        SendEmail = true,
        RequirePasswordChange = true
    };

    private static List<Guid> ParseTenantIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split([',', '\n', '\r', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }
}
