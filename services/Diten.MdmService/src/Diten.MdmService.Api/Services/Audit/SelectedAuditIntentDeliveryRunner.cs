using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Infrastructure.Audit;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.Audit;

/// <summary>Isolated, one-shot delivery of an immutable process-authorized selection. Never starts an API host.</summary>
public sealed class SelectedAuditIntentDeliveryRunner
{
    public const int MaximumMarkerCharacters = 1024;
    private readonly SelectedAuditIntentDeliveryOptions _options;
    private readonly Func<(string Sid, string Account)> _operator;
    private readonly Func<SelectedAuditIntentDeliveryOptions, AuditIntentDeliveryProcessor> _processorFactory;
    private int _attempted;

    /// <summary>Test seam. The production entry point supplies real Windows identity and fixed minimal composition.</summary>
    public SelectedAuditIntentDeliveryRunner(SelectedAuditIntentDeliveryOptions options,
        Func<(string Sid, string Account)> operatorIdentity,
        Func<SelectedAuditIntentDeliveryOptions, AuditIntentDeliveryProcessor> processorFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _operator = operatorIdentity ?? throw new ArgumentNullException(nameof(operatorIdentity));
        _processorFactory = processorFactory ?? throw new ArgumentNullException(nameof(processorFactory));
    }

    public static async Task RunFromProcessAsync(CancellationToken cancellationToken)
    {
        ServiceProvider? composition = null;
        try
        {
            // Program dispatches before WebApplication.CreateBuilder. Recheck the exact invocation here.
            if (!SelectedAuditIntentDeliveryCommandLine.IsRequested(Environment.GetCommandLineArgs().Skip(1))
                || !Console.IsInputRedirected) throw Denied();
            var options = SelectedAuditIntentDeliveryOptions.CaptureProcess();
            var runner = new SelectedAuditIntentDeliveryRunner(options, ReadWindowsOperator, captured =>
            {
                composition = CreateComposition(captured);
                return composition.GetRequiredService<AuditIntentDeliveryProcessor>();
            });
            using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false, true),
                detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
            Environment.ExitCode = await runner.RunAsync(input, Console.Out, cancellationToken);
        }
        catch (Exception)
        {
            // Never emit configuration, credentials, raw markers, HTTP bodies or source payloads.
            Console.Error.WriteLine("SELECTED_AUDIT_INTENT_FAILED_CLOSED");
            Environment.ExitCode = 2;
        }
        finally
        {
            if (composition is not null) await composition.DisposeAsync();
        }
    }

    public async Task<int> RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (Interlocked.Exchange(ref _attempted, 1) != 0) throw Denied();
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overall.CancelAfter(_options.OverallBudget);
        try
        {
            RequireOperator();
            using (var markerBudget = CancellationTokenSource.CreateLinkedTokenSource(overall.Token))
            {
                markerBudget.CancelAfter(_options.MarkerBudget);
                await RequireMarkerAsync(input, markerBudget.Token);
            }
            RequireOperator();
            overall.Token.ThrowIfCancellationRequested();
            // Factory invocation is after both authority gates: even construction cannot contact Mongo/HTTP earlier.
            var processor = _processorFactory(_options);
            RequireOperator();
            var result = await processor.ProcessSelectedAsync(_options.Selection,
                "selected-audit:" + _options.Selection.ExecutionId.ToString("D"), _options.LeaseDuration,
                _options.RetryDelay, _options.MaximumAttempts, overall.Token);
            var complete = result.Receipts.Count == _options.Selection.Items.Count
                && result.Batch.ClaimConflicts == 0 && result.Batch.DeadLettered == 0 && result.Batch.RetryScheduled == 0;
            // Source ActorId and business Version are never supplied or rewritten by this runner.
            await output.WriteLineAsync(JsonSerializer.Serialize(new
            {
                executionId = _options.Selection.ExecutionId,
                tenantId = _options.Selection.TenantId,
                selected = _options.Selection.Items.Count,
                verifiedReceipts = result.Receipts.Count,
                status = complete ? "SELECTED_DURABLE_RECEIPTS_VERIFIED" : "SELECTED_DELIVERY_INCOMPLETE"
            }));
            return complete ? 0 : 2;
        }
        catch (Exception)
        {
            await output.WriteLineAsync("SELECTED_AUDIT_INTENT_FAILED_CLOSED");
            return 2;
        }
    }

    private void RequireOperator()
    {
        var identity = _operator();
        if (!string.Equals(identity.Sid, _options.OperatorSid, StringComparison.Ordinal)
            || !string.Equals(identity.Account, _options.OperatorAccount, StringComparison.Ordinal)) throw Denied();
    }

    private static (string Sid, string Account) ReadWindowsOperator()
    {
        if (!OperatingSystem.IsWindows()) throw Denied();
        using var identity = WindowsIdentity.GetCurrent();
        if (!identity.IsAuthenticated || identity.User is null) throw Denied();
        return (identity.User.Value, identity.Name);
    }

    private async Task RequireMarkerAsync(TextReader input, CancellationToken cancellationToken)
    {
        // EOF frames the one-shot stdin envelope. One trailing LF/CRLF is permitted; a second line is not.
        // A custom/synchronous reader cannot block the caller past the budget. At most one bounded reader is started.
        var read = Task.Run(async () =>
        {
            var buffer = new char[MaximumMarkerCharacters + 3];
            try
            {
                var count = 0;
                while (count < buffer.Length)
                {
                    var received = await input.ReadAsync(buffer.AsMemory(count, buffer.Length - count), cancellationToken);
                    if (received == 0) break;
                    count += received;
                }
                if (count == buffer.Length) throw Denied();
                if (count > 0 && buffer[count - 1] == '\n')
                {
                    count--;
                    if (count > 0 && buffer[count - 1] == '\r') count--;
                }
                if (count is < 1 or > MaximumMarkerCharacters) throw Denied();
                for (var i = 0; i < count; i++) if (char.IsControl(buffer[i])) throw Denied();
                var bytes = Encoding.UTF8.GetBytes(buffer, 0, count);
                try { return SHA256.HashData(bytes); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }
            finally { Array.Clear(buffer); }
        }, CancellationToken.None);
        byte[]? digest = null;
        try
        {
            digest = await read.WaitAsync(cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(_options.MarkerSha256))) throw Denied();
        }
        finally
        {
            if (digest is not null) CryptographicOperations.ZeroMemory(digest);
            // Observe a late read failure after cancellation without logging its potentially sensitive message.
            _ = read.ContinueWith(task =>
            {
                if (task.IsFaulted) _ = task.Exception;
                else if (task.Status == TaskStatus.RanToCompletion) CryptographicOperations.ZeroMemory(task.Result);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private static ServiceProvider CreateComposition(SelectedAuditIntentDeliveryOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOptions<AuthTrustedSourceAuditServiceIdentityProviderOptions>>(Options.Create(options.IdentityOptions));
        services.AddSingleton<IOptions<TrustedSourceAuditIntentClientOptions>>(Options.Create(options.ClientOptions));
        services.AddHttpClient(nameof(AuthTrustedSourceAuditServiceIdentityProvider), client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false })
            .RedactLoggedHeaders([AuthTrustedSourceAuditServiceIdentityProvider.ClientIdHeader,
                AuthTrustedSourceAuditServiceIdentityProvider.ClientSecretHeader, "Authorization"]);
        services.AddHttpClient(nameof(PlatformTrustedSourceAuditIntentClient), client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false })
            .RedactLoggedHeaders(["Authorization", AuthTrustedSourceAuditServiceIdentityProvider.ClientSecretHeader]);
        services.AddSingleton<ITrustedSourceAuditServiceIdentityProvider, AuthTrustedSourceAuditServiceIdentityProvider>();
        services.AddSingleton<ITrustedSourceAuditIntentClient, PlatformTrustedSourceAuditIntentClient>();
        services.AddSingleton<Diten.MdmService.Domain.Repositories.IAuditIntentDeliveryRepository>(_ =>
            AuditIntentDeliveryRepository.CreateSelected(options.MongoConnectionString, options.DatabaseName,
                options.Selection, TimeProvider.System));
        services.AddSingleton<AuditIntentDeliveryProcessor>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static InvalidOperationException Denied() => new("SELECTED_AUDIT_INTENT_AUTHORITY_DENIED");
}
