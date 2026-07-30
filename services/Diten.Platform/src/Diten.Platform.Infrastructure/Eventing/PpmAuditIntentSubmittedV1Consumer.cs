using System.Diagnostics.Metrics;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Eventing;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.Eventing;

internal sealed class PpmAuditIntentSubmittedV1Consumer : IConsumer<EventTransportMessage>
{
    private readonly PpmAuditConsumerProcessor _processor;

    public PpmAuditIntentSubmittedV1Consumer(PpmAuditConsumerProcessor processor)
    {
        _processor = processor;
    }

    public Task Consume(ConsumeContext<EventTransportMessage> context) =>
        _processor.ProcessAsync(
            context.Message,
            context.Headers.Get<string>(PpmAuditSignatureVerifier.SchemeHeader),
            context.Headers.Get<string>(PpmAuditSignatureVerifier.KeyIdHeader),
            context.Headers.Get<string>(PpmAuditSignatureVerifier.SignatureHeader),
            context.GetRetryAttempt(),
            context.CancellationToken);
}

internal interface IPpmAuditDeadLetterObserver
{
    void Record(EventTransportMessage message, Exception exception);
}

internal sealed class PpmAuditDeadLetterObserver : IPpmAuditDeadLetterObserver
{
    private static readonly Meter Meter = new("Diten.Platform.Eventing");
    private static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>("event.deadlettered");
    private readonly ILogger<PpmAuditIntentSubmittedV1Consumer> _logger;

    public PpmAuditDeadLetterObserver(ILogger<PpmAuditIntentSubmittedV1Consumer> logger)
    {
        _logger = logger;
    }

    public void Record(EventTransportMessage message, Exception exception)
    {
        DeadLettered.Add(1,
            new KeyValuePair<string, object?>("event_name", message.EventName),
            new KeyValuePair<string, object?>("consumer", PpmAuditAcceptanceRepository.ConsumerName));
        _logger.LogError(
            exception,
            "event.deadlettered EventId={EventId} EventName={EventName} TenantId={TenantId} ConsumerName={ConsumerName} ErrorType={ErrorType}",
            message.EventId,
            message.EventName,
            message.TenantId,
            PpmAuditAcceptanceRepository.ConsumerName,
            exception.GetType().Name);
    }
}

internal sealed class PpmAuditConsumerProcessor
{
    private readonly PpmAuditSignatureVerifier _signatureVerifier;
    private readonly IPpmAuditAcceptanceRepository _repository;
    private readonly Diten.Platform.Common.Tenancy.ITenantContext _tenantContext;
    private readonly IPpmAuditDeadLetterObserver _deadLetterObserver;
    private readonly ILogger<PpmAuditConsumerProcessor> _logger;

    public PpmAuditConsumerProcessor(
        PpmAuditSignatureVerifier signatureVerifier,
        IPpmAuditAcceptanceRepository repository,
        Diten.Platform.Common.Tenancy.ITenantContext tenantContext,
        IPpmAuditDeadLetterObserver deadLetterObserver,
        ILogger<PpmAuditConsumerProcessor> logger)
    {
        _signatureVerifier = signatureVerifier;
        _repository = repository;
        _tenantContext = tenantContext;
        _deadLetterObserver = deadLetterObserver;
        _logger = logger;
    }

    public async Task ProcessAsync(
        EventTransportMessage message,
        string? signatureScheme,
        string? keyId,
        string? signature,
        int retryAttempt,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(message.EventName, PpmAuditIntentParser.EventName, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var intent = PpmAuditIntentParser.Parse(message);
            _signatureVerifier.Verify(
                message,
                intent,
                signatureScheme,
                keyId,
                signature);

            using var tenantScope = TenantScope.Begin(_tenantContext, message.TenantId!.Value);
            var result = await _repository.AcceptAsync(message, intent, cancellationToken);
            _logger.LogInformation(
                "event.ppm_audit.accepted EventId={EventId} TenantId={TenantId} Result={Result} PayloadSha256={PayloadSha256}",
                message.EventId,
                message.TenantId,
                result,
                intent.PayloadSha256);
        }
        catch (PpmAuditContractException ex)
        {
            _deadLetterObserver.Record(message, ex);
            throw;
        }
        catch (Exception ex)
        {
            var transient = ex as PpmAuditTransientException ?? new PpmAuditTransientException(ex);
            if (retryAttempt < PpmAuditRetryPolicy.RetryCount)
            {
                throw transient;
            }

            var exhausted = new PpmAuditRetriesExhaustedException(
                transient.InnerException ?? transient);
            _deadLetterObserver.Record(message, exhausted);
            throw exhausted;
        }
    }
}

internal sealed class PpmAuditIntentSubmittedV1ConsumerDefinition
    : ConsumerDefinition<PpmAuditIntentSubmittedV1Consumer>
{
    internal const int TotalAttempts = 5;
    internal const int RetryCount = PpmAuditRetryPolicy.RetryCount;

    public PpmAuditIntentSubmittedV1ConsumerDefinition()
    {
        EndpointName = "platform-ppm-audit-intent-v1";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<PpmAuditIntentSubmittedV1Consumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(retry =>
            ConfigureRetry(retry, PpmAuditRetryPolicy.CreateDelays()));
    }

    internal static void ConfigureRetry(IRetryConfigurator retry, params TimeSpan[] intervals)
    {
        retry.Handle<PpmAuditTransientException>();
        retry.Ignore<PpmAuditContractException>();
        retry.Intervals(intervals);
    }
}
