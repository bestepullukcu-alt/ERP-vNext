using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/v1/reference-data/verified-market")]
public sealed class InternalVerifiedMarketReferenceDataController : CustomBaseController
{
    internal const int MaximumResolveRequestBodyBytes = 1024;
    private const int MaximumMarketCodeLength = 16;
    private readonly IMediator _mediator;
    private readonly VerifiedReferenceDataRequestExecutor _requestExecutor;

    public InternalVerifiedMarketReferenceDataController(IMediator mediator,
        IVerifiedGskuResolverCredentialAuthenticator credentialAuthenticator,
        IVerifiedGskuResolverJwtTenantContext jwtTenantContext,
        IVerifiedReferenceDataServiceTenantContext serviceTenantContext,
        ITenantContext tenantContext)
    {
        _mediator = mediator;
        _requestExecutor = new VerifiedReferenceDataRequestExecutor(
            credentialAuthenticator,
            jwtTenantContext,
            serviceTenantContext,
            tenantContext);
    }

    [HttpPost("resolve")]
    public Task<IActionResult> Resolve(CancellationToken cancellationToken) =>
        _requestExecutor.ExecuteAsync(HttpContext, cancellationToken, async (_, token) =>
        {
            if (Request.Query.Count > 0)
            {
                return ResolveFailure(409, "REFERENCE_CONTRACT_MISMATCH");
            }

            var marketCode = await ParseResolveRequestAsync(
                Request.Body,
                Request.ContentLength,
                token);
            if (marketCode is null)
            {
                return ResolveFailure(409, "REFERENCE_CONTRACT_MISMATCH");
            }

            return CreateActionResultInstance(await _mediator.Send(
                new ResolveVerifiedMarketReferenceDataQuery(marketCode),
                token));
        }, ResolveFailure);

    [HttpPost("enumerate-active")]
    public Task<IActionResult> EnumerateActive(CancellationToken cancellationToken) =>
        _requestExecutor.ExecuteInteractiveOnlyAsync(HttpContext, cancellationToken, async (_, token) =>
        {
            if (Request.Query.Count > 0 || Request.ContentLength is > 0 || Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                return EnumerationFailure(409, "REFERENCE_CONTRACT_MISMATCH");
            }
            return CreateActionResultInstance(await _mediator.Send(new EnumerateVerifiedMarketsQuery(), token));
        }, EnumerationFailure);

    internal static async Task<string?> ParseResolveRequestAsync(
        Stream body,
        long? contentLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (contentLength is < 0 or > MaximumResolveRequestBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream(MaximumResolveRequestBodyBytes);
        var chunk = new byte[256];
        while (true)
        {
            var read = await body.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumResolveRequestBodyBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 2
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? marketCode = null;
            var count = 0;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                count++;
                if (!string.Equals(property.Name, "market_code", StringComparison.Ordinal)
                    || property.Value.ValueKind != JsonValueKind.String
                    || marketCode is not null)
                {
                    return null;
                }

                marketCode = property.Value.GetString();
            }

            return count == 1
                && marketCode is { Length: > 0 and <= MaximumMarketCodeLength }
                && string.Equals(marketCode, marketCode.Trim(), StringComparison.Ordinal)
                && !marketCode.Any(char.IsControl)
                    ? marketCode
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private IActionResult ResolveFailure(int statusCode, string code) => CreateActionResultInstance(
        Response<BusinessReferenceDataVerifiedMarketResolveResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));
    private IActionResult EnumerationFailure(int statusCode, string code) => CreateActionResultInstance(
        Response<BusinessReferenceDataVerifiedMarketsResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));
}
