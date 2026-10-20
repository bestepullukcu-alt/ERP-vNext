using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.BuildingBlocks.Auditing.Tests;

/// <summary>A response envelope of the shape every service uses (IsSuccessful / StatusCode / Errors / Data).</summary>
public sealed class Envelope<T>
{
    public T? Data { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    public int StatusCode { get; init; }
    public bool IsSuccessful { get; init; }

    public static Envelope<T> Ok(T data) => new() { Data = data, StatusCode = 200, IsSuccessful = true };

    public static Envelope<T> Fail(int status, params string[] errors) => new() { Errors = errors, StatusCode = status, IsSuccessful = false };
}

public sealed record ThingDto(Guid Id, int Version);

public sealed record RenameThingCommand(Guid ThingId, string Name) : IRequest<Envelope<ThingDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => new(AuditCategory.Crm, AuditOperation.Update, "Thing", "MOD-0155", ThingId);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.Changed("name");
        details.Set("token", "must-never-leave");
        if (response is Envelope<ThingDto> { Data: { } data })
        {
            details.ObjectVersion = data.Version;
        }
    }
}

public sealed record CreateThingCommand(string Name) : IRequest<Envelope<ThingDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => new(AuditCategory.Crm, AuditOperation.Create, "Thing", "MOD-0155");

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        if (response is Envelope<ThingDto> { Data: { } data })
        {
            details.EntityId = data.Id;
        }
    }
}

public sealed record UnmarkedCommand(string Name) : IRequest<Envelope<ThingDto>>;

public sealed class RecordingForwarder : IPlatformAuditForwarder
{
    public List<AuditForwardRequest> Requests { get; } = [];
    public Exception? Throw { get; init; }

    public Task<AuditForwardResult> ForwardAsync(AuditForwardRequest request, CancellationToken cancellationToken = default)
    {
        if (Throw is not null)
        {
            throw Throw;
        }

        Requests.Add(request);
        return Task.FromResult(AuditForwardResult.Sent);
    }
}

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

public sealed class ListLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
}

public sealed class FixedCallContext(AuditCaller caller) : IAuditCallContext
{
    public AuditCaller Resolve() => caller;
}

public sealed class CapturingHandler(HttpStatusCode status = HttpStatusCode.Accepted) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Add((request, body));
        return new HttpResponseMessage(status);
    }

    public JsonElement SingleBody() => JsonDocument.Parse(Assert.Single(Calls).Body).RootElement;
}

public sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
