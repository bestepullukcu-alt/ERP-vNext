using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditBehaviorTests
{
    [Fact]
    public async Task UnmarkedRequest_ShouldNotBeAudited()
    {
        var auditService = new CapturingAuditService();
        var behavior = CreateBehavior<UnmarkedCommand>(auditService);

        var response = await behavior.Handle(
            new UnmarkedCommand(),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(auditService.Requests);
    }

    [Fact]
    public async Task Query_ShouldNotBeAuditedByDefault()
    {
        var auditService = new CapturingAuditService();
        var behavior = CreateBehavior<AuditableQuery>(auditService);

        var response = await behavior.Handle(
            new AuditableQuery(),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(auditService.Requests);
    }

    [Fact]
    public async Task AuditableCommandSuccess_ShouldAppendSucceededAudit()
    {
        var auditService = new CapturingAuditService();
        var behavior = CreateBehavior<AuditableCommand>(auditService);

        var response = await behavior.Handle(
            new AuditableCommand(),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var appendRequest = Assert.Single(auditService.Requests);
        Assert.Equal(AuditOutcome.Succeeded, appendRequest.Outcome);
        Assert.Equal(AuditCategory.Security, appendRequest.Category);
        Assert.Equal(AuditOperation.Update, appendRequest.Operation);
        Assert.Equal("TestEntity", appendRequest.EntityType);
    }

    [Fact]
    public async Task ExcludedCommand_ShouldNotBeAudited()
    {
        var auditService = new CapturingAuditService();
        var behavior = CreateBehavior<ExcludedCommand>(auditService);

        var response = await behavior.Handle(
            new ExcludedCommand(),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(auditService.Requests);
    }

    [Fact]
    public async Task ExcludedAuditableCommand_ShouldNotBeAuditedAndShouldLogWarning()
    {
        var auditService = new CapturingAuditService();
        var logger = new CapturingLogger<AuditBehavior<ExcludedAuditableCommand, Response<NoContent>>>();
        var behavior = CreateBehavior<ExcludedAuditableCommand>(auditService, logger: logger);

        var response = await behavior.Handle(
            new ExcludedAuditableCommand(),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(auditService.Requests);
        Assert.Contains(logger.Entries, entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("Exclusion wins", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LogicalResponseFail_ShouldAppendFailedAudit()
    {
        var auditService = new CapturingAuditService();
        var behavior = CreateBehavior<AuditableCommand>(auditService);

        var response = await behavior.Handle(
            new AuditableCommand(),
            () => Task.FromResult(Response<NoContent>.Fail("Business rule failed.", 409)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        var appendRequest = Assert.Single(auditService.Requests);
        Assert.Equal(AuditOutcome.Failed, appendRequest.Outcome);
        Assert.Equal("LogicalFailure", appendRequest.Metadata["AuditBehaviorOutcome"]);
        Assert.Equal(409, appendRequest.Metadata["ResponseStatusCode"]);
    }

    [Fact]
    public async Task RuntimeException_ShouldAppendFailedAuditAndRethrow()
    {
        var auditService = new CapturingAuditService();
        var behavior = CreateBehavior<AuditableCommand>(auditService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new AuditableCommand(),
                () => throw new InvalidOperationException("raw password secret should not be copied"),
                CancellationToken.None));

        Assert.Contains("raw password secret", exception.Message, StringComparison.Ordinal);
        var appendRequest = Assert.Single(auditService.Requests);
        Assert.Equal(AuditOutcome.Failed, appendRequest.Outcome);
        Assert.Equal("RuntimeException", appendRequest.Metadata["AuditBehaviorOutcome"]);
        Assert.Equal(nameof(InvalidOperationException), appendRequest.Metadata["ExceptionType"]);
        Assert.Equal("Unhandled exception during audited request.", appendRequest.Metadata["ExceptionMessage"]);
        Assert.DoesNotContain("raw password secret", string.Join('|', appendRequest.Metadata.Values));
        Assert.DoesNotContain("StackTrace", appendRequest.Metadata.Keys);
    }

    [Fact]
    public async Task RejectedDefault_ShouldNotBreakBusinessCommand()
    {
        var auditService = new CapturingAuditService(AuditAppendResult.Rejected("missing tenant"));
        var behavior = CreateBehavior<AuditableCommand>(auditService);

        var response = await behavior.Handle(
            new AuditableCommand(),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(auditService.Requests);
    }

    [Fact]
    public async Task RejectedCriticalWithinCriticalCategories_ShouldBreakBusinessCommand()
    {
        var auditService = new CapturingAuditService(AuditAppendResult.RejectedCritical("critical audit rejected"));
        var options = new AuditBehaviorOptions();
        options.CriticalCategories.Add(AuditCategory.Security);
        var behavior = CreateBehavior<AuditableCommand>(auditService, options);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new AuditableCommand(),
                () => Task.FromResult(Response<NoContent>.Success()),
                CancellationToken.None));
    }

    [Fact]
    public async Task CriticalCategoriesEmpty_ShouldNotBreakForAnyRejection()
    {
        var auditService = new CapturingAuditService(AuditAppendResult.RejectedCritical("critical audit rejected"));
        var behavior = CreateBehavior<AuditableCommand>(auditService, new AuditBehaviorOptions());

        var response = await behavior.Handle(
            new AuditableCommand(),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(auditService.Requests);
    }

    [Fact]
    public async Task SkippedRecursionResult_ShouldNotBreakBusinessCommand()
    {
        await AssertControlledResultDoesNotBreakAsync(
            AuditAppendResult.SkippedRecursion("guard active"),
            AuditAppendStatus.SkippedRecursion);
    }

    [Fact]
    public async Task DuplicateResult_ShouldNotBreakBusinessCommand()
    {
        await AssertControlledResultDoesNotBreakAsync(
            AuditAppendResult.Duplicate("audit:duplicate"),
            AuditAppendStatus.Duplicate);
    }

    [Fact]
    public async Task EnqueueFailedResult_ShouldNotBreakBusinessCommand()
    {
        await AssertControlledResultDoesNotBreakAsync(
            AuditAppendResult.EnqueueFailed("audit:failed", "enqueue failed"),
            AuditAppendStatus.EnqueueFailed);
    }

    private static async Task AssertControlledResultDoesNotBreakAsync(
        AuditAppendResult appendResult,
        AuditAppendStatus expectedStatus)
    {
        var auditService = new CapturingAuditService(appendResult);
        var behavior = CreateBehavior<AuditableCommand>(auditService);

        var response = await behavior.Handle(
            new AuditableCommand(),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(expectedStatus, auditService.Results.Single().Status);
    }

    [Theory]
    [InlineData(typeof(UpdateTenantHealthMetricsCommand))]
    [InlineData(typeof(RetryFailedPaymentCommand))]
    [InlineData(typeof(RegisterInternalUserCommand))]
    [InlineData(typeof(SeedCustomerDataCommand))]
    public void BusinessCommandsContainingNarrowingKeywords_ShouldNotBeAutoExcluded(Type requestType)
    {
        var options = new AuditBehaviorOptions();

        Assert.False(options.IsAutoExcluded(requestType),
            $"{requestType.Name} should not be auto-excluded by narrow audit behavior options.");
    }

    [Theory]
    [InlineData(typeof(AuditOutboxFlushCommand))]
    [InlineData(typeof(EnqueueAuditEventInternalCommand))]
    [InlineData(typeof(HeartbeatPingCommand))]
    [InlineData(typeof(DeadLetterReplayCommand))]
    [InlineData(typeof(AuditAppendRetryCommand))]
    public void InfrastructureRequests_ShouldRemainAutoExcluded(Type requestType)
    {
        var options = new AuditBehaviorOptions();

        Assert.True(options.IsAutoExcluded(requestType),
            $"{requestType.Name} should remain auto-excluded for audit pipeline safety.");
    }

    [Fact]
    public async Task AuditableCommandWithoutMetadataProvider_ShouldThrowByDefault()
    {
        var auditService = new CapturingAuditService();
        var behavior = CreateBehavior<AuditableCommandMissingMetadataProvider>(auditService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new AuditableCommandMissingMetadataProvider(),
                () => Task.FromResult(Response<NoContent>.Success()),
                CancellationToken.None));

        Assert.Contains("must implement IAuditMetadataProvider", exception.Message, StringComparison.Ordinal);
        Assert.Empty(auditService.Requests);
    }

    [Fact]
    public async Task AuditableCommandWithoutMetadataProvider_ShouldWarnAndSkipWhenRequireMetadataProviderDisabled()
    {
        var auditService = new CapturingAuditService();
        var options = new AuditBehaviorOptions { RequireMetadataProvider = false };
        var logger = new CapturingLogger<AuditBehavior<AuditableCommandMissingMetadataProvider, Response<NoContent>>>();
        var behavior = CreateBehavior<AuditableCommandMissingMetadataProvider>(auditService, options, logger);

        var response = await behavior.Handle(
            new AuditableCommandMissingMetadataProvider(),
            () => Task.FromResult(Response<NoContent>.Success()),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(auditService.Requests);
        Assert.Contains(logger.Entries, entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("does not provide audit metadata", StringComparison.OrdinalIgnoreCase));
    }

    private static AuditBehavior<TRequest, Response<NoContent>> CreateBehavior<TRequest>(
        CapturingAuditService auditService,
        AuditBehaviorOptions? options = null,
        CapturingLogger<AuditBehavior<TRequest, Response<NoContent>>>? logger = null)
        where TRequest : notnull
    {
        return new AuditBehavior<TRequest, Response<NoContent>>(
            auditService,
            options ?? new AuditBehaviorOptions(),
            logger ?? new CapturingLogger<AuditBehavior<TRequest, Response<NoContent>>>());
    }

    private static AuditRequestMetadata Metadata()
    {
        return new AuditRequestMetadata(
            AuditCategory.Security,
            AuditOperation.Update,
            "TestEntity",
            EntityId: Guid.Parse("f755c10d-a018-41f0-a250-f1e37909a76f"),
            SourceModule: "AuditBehaviorTests",
            CorrelationId: Guid.Parse("aa6b15d7-10fc-4673-9bfe-18a3dbefddc5"));
    }

    private sealed record UnmarkedCommand : IRequest<Response<NoContent>>;

    private sealed record ExcludedCommand : IRequest<Response<NoContent>>, IAuditExcludedRequest;

    private sealed record AuditableQuery : IRequest<Response<NoContent>>, IAuditableRequest, IAuditMetadataProvider
    {
        public AuditRequestMetadata GetAuditMetadata()
        {
            return Metadata();
        }
    }

    private sealed record AuditableCommand : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
    {
        public AuditRequestMetadata GetAuditMetadata()
        {
            return Metadata();
        }
    }

    private sealed record ExcludedAuditableCommand
        : IRequest<Response<NoContent>>, IAuditableCommand, IAuditExcludedRequest, IAuditMetadataProvider
    {
        public AuditRequestMetadata GetAuditMetadata()
        {
            return Metadata();
        }
    }

    private sealed record AuditableCommandMissingMetadataProvider : IRequest<Response<NoContent>>, IAuditableCommand;

    private sealed record UpdateTenantHealthMetricsCommand : IRequest<Response<NoContent>>;
    private sealed record RetryFailedPaymentCommand : IRequest<Response<NoContent>>;
    private sealed record RegisterInternalUserCommand : IRequest<Response<NoContent>>;
    private sealed record SeedCustomerDataCommand : IRequest<Response<NoContent>>;
    private sealed record AuditOutboxFlushCommand : IRequest<Response<NoContent>>;
    private sealed record EnqueueAuditEventInternalCommand : IRequest<Response<NoContent>>;
    private sealed record HeartbeatPingCommand : IRequest<Response<NoContent>>;
    private sealed record DeadLetterReplayCommand : IRequest<Response<NoContent>>;
    private sealed record AuditAppendRetryCommand : IRequest<Response<NoContent>>;

    private sealed class CapturingAuditService : IAuditService
    {
        private readonly Queue<AuditAppendResult> _configuredResults;

        public CapturingAuditService(params AuditAppendResult[] configuredResults)
        {
            _configuredResults = new Queue<AuditAppendResult>(configuredResults);
        }

        public List<AuditAppendRequest> Requests { get; } = [];
        public List<AuditAppendResult> Results { get; } = [];

        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            var result = _configuredResults.Count > 0
                ? _configuredResults.Dequeue()
                : AuditAppendResult.Queued("audit:test");
            Results.Add(result);
            return Task.FromResult(result);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
