using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Features.DocumentManagementApproval.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.Auth;

public sealed class AuthServiceApprovalRoleDirectory : IApprovalRoleDirectory
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly HttpClient _httpClient;
    private readonly ITenantContext _tenantContext;
    private readonly AuthServiceOptions _options;

    public AuthServiceApprovalRoleDirectory(
        HttpClient httpClient,
        ITenantContext tenantContext,
        IOptions<AuthServiceOptions> options)
    {
        _httpClient = httpClient;
        _tenantContext = tenantContext;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<IReadOnlyDictionary<string, ApprovalDirectoryRole>> ResolveAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken ct = default)
    {
        if (_tenantContext.TenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(_options.InternalApiKey)
            || roleNames.Count == 0)
        {
            return new Dictionary<string, ApprovalDirectoryRole>(StringComparer.OrdinalIgnoreCase);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/roles/resolve")
        {
            Content = JsonContent.Create(new ResolveRolesRequest(_tenantContext.TenantId, roleNames))
        };
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return Empty();
            }

            var roles = await response.Content.ReadFromJsonAsync<List<ResolvedRoleResponse>>(cancellationToken: ct);
            return roles?.ToDictionary(
                       role => role.Name,
                       role => new ApprovalDirectoryRole(role.Id, role.Name, role.DisplayName),
                       StringComparer.OrdinalIgnoreCase)
                   ?? Empty();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return Empty();
        }
    }

    public async Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        if (_tenantContext.TenantId == Guid.Empty
            || userId == Guid.Empty
            || roleId == Guid.Empty
            || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/roles/authorize")
        {
            Content = JsonContent.Create(new AuthorizeRoleRequest(_tenantContext.TenantId, userId, roleId))
        };
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthorizeRoleResponse>(cancellationToken: ct);
            return result?.Authorized == true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return false;
        }
    }

    private static IReadOnlyDictionary<string, ApprovalDirectoryRole> Empty() =>
        new Dictionary<string, ApprovalDirectoryRole>(StringComparer.OrdinalIgnoreCase);

    private sealed record ResolveRolesRequest(Guid TenantId, IReadOnlyCollection<string> Names);
    private sealed record ResolvedRoleResponse(Guid Id, string Name, string DisplayName);
    private sealed record AuthorizeRoleRequest(Guid TenantId, Guid UserId, Guid RoleId);
    private sealed record AuthorizeRoleResponse(bool Authorized);
}
