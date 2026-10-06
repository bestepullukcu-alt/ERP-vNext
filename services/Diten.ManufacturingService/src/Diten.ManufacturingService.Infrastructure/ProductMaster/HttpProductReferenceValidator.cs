using System.Net.Http.Json;
using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace Diten.ManufacturingService.Infrastructure.ProductMaster;

/// <summary>
/// PRODUCT-MASTER (MOD-0290) frozen contract <c>POST {BaseUrl}/validate</c> istemcisi — <c>ProductMaster:Mode=Http</c>
/// iken kayıtlıdır. Çağıranın taşıyıcı belirteci ve kiracı başlıkları iletilir. Yanıt alınamazsa kimlikler BİLİNMEYEN
/// sayılmaz; 503 döner: erişilemeyen ana veri "her şey bilinmiyor" diye 422'ye çevrilmez.
/// </summary>
public sealed class HttpProductReferenceValidator(HttpClient http, IHttpContextAccessor accessor) : IProductReferenceValidator
{
    private static readonly string[] ForwardedHeaders = ["Authorization", "X-Tenant-Id", "X-Legal-Entity-Id", "X-Correlation-Id"];

    public async Task<IReadOnlyList<Guid>> GetUnknownItemIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "validate")
        {
            Content = JsonContent.Create(new { refs = itemIds.Select(id => new { itemId = id }).ToArray() })
        };
        var incoming = accessor.HttpContext?.Request.Headers;
        foreach (var header in ForwardedHeaders)
        {
            if (incoming is not null && incoming.TryGetValue(header, out var value))
            {
                request.Headers.TryAddWithoutValidation(header, value.ToArray());
            }
        }

        try
        {
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<ValidateResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("PRODUCT-MASTER validate returned no body.");
            var known = body.Results.Where(r => r.Exists && r.ItemId is not null).Select(r => r.ItemId!.Value).ToHashSet();
            return itemIds.Where(id => !known.Contains(id)).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            throw new BomPersistenceUnavailableException("PRODUCT-MASTER validate is unavailable.", ex);
        }
    }

    private sealed record ValidateResponse(List<ValidateResult> Results);

    private sealed record ValidateResult(Guid? ItemId, bool Exists);
}
