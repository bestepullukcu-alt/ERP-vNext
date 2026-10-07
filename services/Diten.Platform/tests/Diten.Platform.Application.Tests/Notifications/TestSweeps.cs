using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 — the retry sweep's dependencies are all REQUIRED now (production must never get a silent null). A test that
/// is about something else gets each of them here, explicitly: a mediator that answers nothing, the default retention
/// options, an unresolved tenant context and an effects service with no meeting stores.
/// </summary>
internal static class TestSweeps
{
    public static EmailDispatchSweepJob Create(
        INotificationDispatchRepository dispatches,
        IBackgroundJobScheduler scheduler,
        ILogger<EmailDispatchSweepJob> logger,
        IMediator? mediator = null,
        IOptions<EmailDispatchRetentionOptions>? retention = null,
        ITenantContext? tenantContext = null,
        NotificationPermanentFailureEffects? permanentFailure = null) =>
        new(
            dispatches,
            scheduler,
            logger,
            mediator ?? new NothingMediator(),
            retention ?? Options.Create(new EmailDispatchRetentionOptions()),
            tenantContext ?? new TenantContext(),
            permanentFailure ?? new NotificationPermanentFailureEffects(null, null, null, null));

    private sealed class NothingMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => Task.FromResult(default(TResponse)!);
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}

/// <summary>BL-454 stage D FIX2 K12 — the retry job's ledger is required; a test that is not about it hands this one.</summary>
internal sealed class NoInvitationLedger : Diten.Platform.Application.Contracts.ITenantAdminInvitationLedger
{
    public static readonly NoInvitationLedger Instance = new();

    public Task RecordUndeliveredAsync(Guid tenantId, string adminEmail, string reasonCode, DateTimeOffset invitationQueuedAt, CancellationToken ct) =>
        Task.CompletedTask;
}
