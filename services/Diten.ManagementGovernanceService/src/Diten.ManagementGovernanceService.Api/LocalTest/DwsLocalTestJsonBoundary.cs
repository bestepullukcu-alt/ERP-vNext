using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Api.LocalTest;

public sealed class DwsLocalTestJsonBoundary(RequestDelegate next)
{
    private static readonly PathString DwsPath = new("/api/dws/structures");

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(DwsPath)
            || !HasRequestBody(context.Request)
            || context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true)
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();
        await using var buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
        context.Request.Body.Position = 0;

        if (!IsStrictJson(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length))))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                Response<object>.Fail(DwsErrors.InvalidRequest, StatusCodes.Status400BadRequest),
                context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool HasRequestBody(HttpRequest request) =>
        request.ContentLength is > 0
        || request.Headers.ContainsKey("Transfer-Encoding");

    private static bool IsStrictJson(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
            return true;

        try
        {
            var reader = new Utf8JsonReader(payload, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            var objects = new Stack<HashSet<string>>();
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        objects.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                        break;
                    case JsonTokenType.EndObject:
                        if (objects.Count == 0) return false;
                        objects.Pop();
                        break;
                    case JsonTokenType.PropertyName:
                        if (objects.Count == 0 || !objects.Peek().Add(reader.GetString() ?? string.Empty))
                            return false;
                        break;
                    case JsonTokenType.String:
                        _ = reader.GetString();
                        break;
                }
            }
            return reader.BytesConsumed == payload.Length && objects.Count == 0;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
