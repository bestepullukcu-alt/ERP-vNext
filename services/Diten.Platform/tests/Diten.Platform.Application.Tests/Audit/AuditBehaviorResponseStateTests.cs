using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// MOD-0262-FU08 — the response-state step of the REAL <see cref="AuditBehavior{TRequest,TResponse}"/> (K-6 path b, K-7):
/// asked only on success (AC-4), the command's metadata wins (AC-5), a command without the interface is recorded exactly as
/// before even when its response carries an id (AC-6), a throwing provider keeps the answer and the record (AC-7).
/// Fake commands, a capturing <see cref="IAuditService"/>.
/// </summary>
public sealed class AuditBehaviorResponseStateTests
{
    private static readonly Guid StateEntity = Guid.Parse("08080808-0000-4000-8000-0000000000e1");
    private static readonly Guid MetadataEntity = Guid.Parse("08080808-0000-4000-8000-0000000000e2");
    private static readonly Guid ResponseId = Guid.Parse("08080808-0000-4000-8000-0000000000e3");
    private static readonly Guid Correlation = Guid.Parse("08080808-0000-4000-8000-0000000000c1");

    [Fact]
    public async Task A_successful_response_gives_the_record_the_providers_entity_id_and_after_state()
    {
        var (behavior, audit, _) = Build<ProvidingCommand>();
        var command = new ProvidingCommand();

        await behavior.Handle(command, () => Task.FromResult(Response<ProbeModel>.Success(new ProbeModel(ResponseId))), CancellationToken.None);

        var request = Assert.Single(audit.Requests);
        Assert.Equal(AuditOutcome.Succeeded, request.Outcome);
        Assert.Equal(StateEntity, request.EntityId);
        Assert.Equal("state", request.AfterState!["From"]);
        Assert.Equal(1, command.Calls);
    }

    [Fact]
    public async Task A_failed_response_never_asks_the_response_state_provider()
    {
        var (behavior, audit, _) = Build<ProvidingCommand>();
        var command = new ProvidingCommand();

        var response = await behavior.Handle(command, () => Task.FromResult(Response<ProbeModel>.Fail("no", 400)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(0, command.Calls);
        var request = Assert.Single(audit.Requests);
        Assert.Equal(AuditOutcome.Failed, request.Outcome);
        Assert.Null(request.EntityId);
        Assert.Null(request.AfterState);
    }

    [Fact]
    public async Task A_runtime_exception_never_asks_the_response_state_provider()
    {
        var (behavior, audit, _) = Build<ProvidingCommand>();
        var command = new ProvidingCommand();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(command, () => throw new InvalidOperationException("boom"), CancellationToken.None));

        Assert.Equal(0, command.Calls);
        var request = Assert.Single(audit.Requests);
        Assert.Equal(AuditOutcome.Failed, request.Outcome);
        Assert.Null(request.EntityId);
        Assert.Null(request.AfterState);
    }

    [Fact]
    public async Task Command_metadata_entity_id_and_after_state_win_over_the_response_state()
    {
        var (behavior, audit, _) = Build<ProvidingCommand>();
        var command = new ProvidingCommand { MetadataEntityId = MetadataEntity, MetadataAfterState = new Dictionary<string, object?> { ["From"] = "metadata" } };

        await behavior.Handle(command, () => Task.FromResult(Response<ProbeModel>.Success(new ProbeModel(ResponseId))), CancellationToken.None);

        var request = Assert.Single(audit.Requests);
        Assert.Equal(MetadataEntity, request.EntityId);
        Assert.Equal("metadata", request.AfterState!["From"]);
    }

    [Fact]
    public async Task A_command_without_the_provider_gets_the_same_append_request()
    {
        var (behavior, audit, _) = Build<PlainCommand>();

        await behavior.Handle(new PlainCommand(), () => Task.FromResult(Response<ProbeModel>.Success(new ProbeModel(ResponseId))), CancellationToken.None);

        var request = Assert.Single(audit.Requests);
        // Field by field what the command's metadata says — the response's ContentId / Id never reaches the record.
        Assert.Equal(Correlation, request.CorrelationId);
        Assert.Equal(nameof(PlainCommand), request.RequestType);
        Assert.Equal(AuditActorType.System, request.ActorType);
        Assert.Equal(AuditCategory.DocumentManagement, request.Category);
        Assert.Equal("Probe", request.EntityType);
        Assert.Null(request.EntityId);
        Assert.Equal(AuditOperation.Create, request.Operation);
        Assert.Equal(AuditOutcome.Succeeded, request.Outcome);
        Assert.Null(request.BeforeState);
        Assert.Null(request.AfterState);
        Assert.Empty(request.Metadata);
        Assert.Equal("Diten.Platform", request.SourceService);
        Assert.Equal("FU08-TEST", request.SourceModule);
        Assert.False(request.IsPlatformGlobal);
        Assert.Null(request.TargetTenantId);
    }

    [Fact]
    public async Task A_throwing_response_state_provider_keeps_the_answer_and_writes_the_record_without_state()
    {
        var (behavior, audit, logs) = Build<ThrowingCommand>();

        var response = await behavior.Handle(new ThrowingCommand(), () => Task.FromResult(Response<ProbeModel>.Success(new ProbeModel(ResponseId))), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ResponseId, response.Data!.ContentId);
        var request = Assert.Single(audit.Requests);
        Assert.Equal(AuditOutcome.Succeeded, request.Outcome);
        Assert.Null(request.EntityId);
        Assert.Null(request.AfterState);
        var warning = Assert.Single(logs.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(nameof(ThrowingCommand), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-detail", warning.Message, StringComparison.Ordinal);
    }

    private static (AuditBehavior<TCommand, Response<ProbeModel>> Behavior, CapturingAudit Audit, CapturingLogger<AuditBehavior<TCommand, Response<ProbeModel>>> Logs) Build<TCommand>()
        where TCommand : notnull
    {
        var audit = new CapturingAudit();
        var logs = new CapturingLogger<AuditBehavior<TCommand, Response<ProbeModel>>>();
        return (new AuditBehavior<TCommand, Response<ProbeModel>>(audit, new AnonymousTenantAuthorizationContext(), new AuditBehaviorOptions(), logs), audit, logs);
    }

    private static AuditRequestMetadata Metadata(Guid? entityId = null, IReadOnlyDictionary<string, object?>? afterState = null) => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "Probe", entityId, SourceModule: "FU08-TEST",
        CorrelationId: Correlation, AfterState: afterState);

    /// <summary>The response has the very properties a reflective reader would look for.</summary>
    public sealed record ProbeModel(Guid ContentId)
    {
        public Guid Id => ContentId;
    }

    public sealed class ProvidingCommand : IRequest<Response<ProbeModel>>, IAuditableCommand, IAuditMetadataProvider,
        IAuditResponseStateProvider<Response<ProbeModel>>
    {
        public int Calls { get; private set; }
        public Guid? MetadataEntityId { get; init; }
        public IReadOnlyDictionary<string, object?>? MetadataAfterState { get; init; }

        public AuditRequestMetadata GetAuditMetadata() => Metadata(MetadataEntityId, MetadataAfterState);

        public AuditResponseState? GetAuditResponseState(Response<ProbeModel> response)
        {
            Calls++;
            return new AuditResponseState(StateEntity, new Dictionary<string, object?> { ["From"] = "state" });
        }
    }

    public sealed class PlainCommand : IRequest<Response<ProbeModel>>, IAuditableCommand, IAuditMetadataProvider
    {
        public AuditRequestMetadata GetAuditMetadata() => Metadata();
    }

    public sealed class ThrowingCommand : IRequest<Response<ProbeModel>>, IAuditableCommand, IAuditMetadataProvider,
        IAuditResponseStateProvider<Response<ProbeModel>>
    {
        public AuditRequestMetadata GetAuditMetadata() => Metadata();

        public AuditResponseState? GetAuditResponseState(Response<ProbeModel> response) =>
            throw new InvalidOperationException("secret-detail");
    }

    private sealed class CapturingAudit : IAuditService
    {
        public List<AuditAppendRequest> Requests { get; } = [];

        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(AuditAppendResult.Queued("key"));
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " | " + exception.Message)));
    }
}
