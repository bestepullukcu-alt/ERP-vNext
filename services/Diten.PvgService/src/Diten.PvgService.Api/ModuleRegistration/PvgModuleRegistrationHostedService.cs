using System.Net.Http.Json;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.PvgService.Api.ModuleRegistration;

/// <summary>
/// The only seam tests use to keep the push fast: the wait between attempts and the timeout of one attempt. The defaults
/// are the pack values (waits of 2, 4, 8 and 16 seconds come from the retry policy; 5 seconds per attempt). It is not a
/// configuration key: the host registers the defaults and only test code replaces them.
/// </summary>
public sealed record PvgModuleRegistrationTiming(Func<TimeSpan, CancellationToken, Task> Delay, TimeSpan AttemptTimeout)
{
    public static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromSeconds(5);

    public static PvgModuleRegistrationTiming Default => new(Task.Delay, DefaultAttemptTimeout);
}

/// <summary>
/// Pushes the closed catalog manifest to Platform's internal endpoint once, at service start (pack "Closed Catalog
/// Registration"). It implements <see cref="IHostedService"/> directly and does not derive from <c>BackgroundService</c>:
/// <see cref="StartAsync"/> starts one task, does not wait for it and returns, so a Platform that is down never blocks the
/// start. <see cref="StopAsync"/> cancels the task and waits for it. The attempts are bounded (5, waits of 2, 4, 8 and 16
/// seconds, 5 seconds per attempt); nothing runs after success or give-up and there is no timer. No exception leaves the
/// task. Log entries carry a safe code, the attempt number and the status code, never the key, the address, an exception
/// object or an exception message. The answer's body is never read.
/// </summary>
public sealed class PvgModuleRegistrationHostedService : IHostedService
{
    public const string SkippedNotConfiguredCode = "PVG_MODULE_REGISTRATION_SKIPPED_NOT_CONFIGURED";
    public const string SkippedProductionLikeCode = "PVG_MODULE_REGISTRATION_SKIPPED_PRODUCTION_LIKE";
    public const string SucceededCode = "PVG_MODULE_REGISTRATION_SUCCEEDED";
    public const string RefusedCode = "PVG_MODULE_REGISTRATION_REFUSED";
    public const string RetryingCode = "PVG_MODULE_REGISTRATION_RETRYING";
    public const string GaveUpCode = "PVG_MODULE_REGISTRATION_GAVE_UP";
    public const string StoppedCode = "PVG_MODULE_REGISTRATION_STOPPED";

    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string RegisterPath = "/api/internal/module-catalog/register-manifest";
    private const int MaxAttempts = 5;

    private readonly IModuleManifestProvider _provider;
    private readonly IPvgModuleRegistrationEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<PvgServiceApiRuntimeOptions> _runtimeOptions;
    private readonly PvgModuleRegistrationTiming _timing;
    private readonly ILogger<PvgModuleRegistrationHostedService> _logger;

    private CancellationTokenSource? _stopping;
    private Task? _push;

    public PvgModuleRegistrationHostedService(
        IModuleManifestProvider provider,
        IPvgModuleRegistrationEnvironment environment,
        IHttpClientFactory httpClientFactory,
        IOptions<PvgServiceApiRuntimeOptions> runtimeOptions,
        PvgModuleRegistrationTiming timing,
        ILogger<PvgModuleRegistrationHostedService> logger)
    {
        _provider = provider;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
        _runtimeOptions = runtimeOptions;
        _timing = timing;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_push is not null)
        {
            return Task.CompletedTask;
        }

        _stopping = new CancellationTokenSource();
        var stopToken = _stopping.Token;
        _push = Task.Run(() => PushAsync(stopToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var push = _push;
        var stopping = _stopping;
        if (push is null || stopping is null)
        {
            return;
        }

        stopping.Cancel();

        try
        {
            await push.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The push task never throws; a host stop token that fires first only abandons the wait. StopAsync does
            // not rethrow.
        }
    }

    // The whole push. It never throws: every exit is a safe log code.
    private async Task PushAsync(CancellationToken stopToken)
    {
        try
        {
            // The production-like check comes first: on such a host the values are not read at all.
            if (_runtimeOptions.Value.IsProductionLike)
            {
                _logger.LogInformation(SkippedProductionLikeCode);
                return;
            }

            var key = _environment.InternalApiKey;
            var baseUrl = _environment.BaseUrl;

            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogInformation(SkippedNotConfiguredCode);
                return;
            }

            if (!IsHttpAbsoluteUri(baseUrl))
            {
                _logger.LogInformation(SkippedNotConfiguredCode);
                return;
            }

            var address = baseUrl!.Trim().TrimEnd('/') + RegisterPath;
            var manifest = _provider.GetManifest();

            var stopped = await ModuleRegistrationRetry.RunAsync(
                attempt: (attemptNumber, ct) => TryRegisterAsync(manifest, key, address, attemptNumber, ct),
                maxAttempts: MaxAttempts,
                backoff: attemptNumber => TimeSpan.FromSeconds(Math.Pow(2, attemptNumber)),
                delay: _timing.Delay,
                stopToken).ConfigureAwait(false);

            if (!stopped)
            {
                _logger.LogWarning(GaveUpCode + " attempts={Attempts}", MaxAttempts);
            }
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
            _logger.LogInformation(StoppedCode);
        }
        catch (Exception)
        {
            // An exception the state table has no code for ends the push. The exception object and its message are
            // not logged: its text can carry the address.
            _logger.LogWarning(GaveUpCode + " attempts={Attempts}", MaxAttempts);
        }
    }

    private static bool IsHttpAbsoluteUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <returns><c>true</c> to STOP (success or a refusal), <c>false</c> to RETRY (connection failure, timeout, 5xx).</returns>
    private async Task<bool> TryRegisterAsync(
        ModuleManifestDocument manifest,
        string key,
        string address,
        int attemptNumber,
        CancellationToken stopToken)
    {
        // The attempt token is linked to the stop token and also carries the attempt timeout, so a timeout and a stop can
        // be told apart: only the stop token says the service is stopping.
        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
        attemptCts.CancelAfter(_timing.AttemptTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, address)
            {
                Content = JsonContent.Create(manifest)
            };
            request.Headers.Add(InternalApiKeyHeader, key);

            var client = _httpClientFactory.CreateClient(PvgModuleRegistrationServiceCollectionExtensions.HttpClientName);
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token)
                .ConfigureAwait(false);

            var status = (int)response.StatusCode;

            if (status is >= 200 and <= 299)
            {
                _logger.LogInformation(SucceededCode + " attempt={Attempt} status={Status}", attemptNumber, status);
                return true;
            }

            if (status >= 500)
            {
                _logger.LogWarning(RetryingCode + " attempt={Attempt} status={Status}", attemptNumber, status);
                return false;
            }

            _logger.LogWarning(RefusedCode + " attempt={Attempt} status={Status}", attemptNumber, status);
            return true;
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(RetryingCode + " attempt={Attempt}", attemptNumber);
            return false;
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning(RetryingCode + " attempt={Attempt}", attemptNumber);
            return false;
        }
    }
}
