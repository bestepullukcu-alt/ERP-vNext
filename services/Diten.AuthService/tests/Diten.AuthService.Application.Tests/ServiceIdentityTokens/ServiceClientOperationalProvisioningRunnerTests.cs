using System.IO.Pipes;
using System.Text.Json;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceClientOperationalProvisioningRunnerTests
{
    [Theory]
    [InlineData("--SERVICE-CLIENT-OPERATIONAL-RUN")]
    [InlineData("--service-client-operational-run=true")]
    [InlineData("--service-client-unknown")]
    public async Task Malformed_command_family_never_falls_through_or_calls_service(string command)
    {
        Assert.True(ServiceClientOperationalProvisioningRunner.IsProcessInvocationRequested([command]));
        var harness = new RunnerHarness();
        Assert.Equal(2, await harness.Run(arguments: [command]));
        Assert.Equal(0, harness.Calls);
    }

    [Fact]
    public async Task Duplicate_conflicting_or_extra_arguments_are_rejected_before_input()
    {
        foreach (var arguments in new[] { new[] { ServiceClientOperationalProvisioningRunner.Mode, ServiceClientOperationalProvisioningRunner.Mode },
                     new[] { ServiceClientOperationalProvisioningRunner.Mode, "--other" }, Array.Empty<string>() })
        {
            var harness = new RunnerHarness();
            Assert.Equal(2, await harness.Run(arguments: arguments));
            Assert.Equal(0, harness.Calls);
        }
        Assert.False(ServiceClientOperationalProvisioningRunner.IsProcessInvocationRequested([]));
    }

    [Theory]
    [InlineData("duplicate-root")]
    [InlineData("unknown-root")]
    [InlineData("case-root")]
    [InlineData("duplicate-command")]
    [InlineData("missing-null")]
    [InlineData("negative-version")]
    [InlineData("fraction-version")]
    [InlineData("noncanonical-guid")]
    [InlineData("trailing")]
    [InlineData("oversize")]
    public async Task Strict_json_rejects_ambiguous_input_without_echoing_credentials(string fault)
    {
        var harness = new RunnerHarness();
        var input = Envelope(harness.Request);
        input = fault switch
        {
            "duplicate-root" => input.Insert(1, "\"accessToken\":\"test-token\","),
            "unknown-root" => input.Insert(1, "\"unknown\":true,"),
            "case-root" => input.Replace("\"accessToken\"", "\"AccessToken\""),
            "duplicate-command" => input.Replace("\"commandId\":", "\"commandId\":\"" + Guid.NewGuid() + "\",\"commandId\":"),
            "missing-null" => input.Replace("\"tenantId\":null,", ""),
            "negative-version" => input.Replace("\"expectedOperationalVersion\":0", "\"expectedOperationalVersion\":-1"),
            "fraction-version" => input.Replace("\"expectedOperationalVersion\":0", "\"expectedOperationalVersion\":0.5"),
            "noncanonical-guid" => input.Replace(harness.Request.CommandId.ToString(), harness.Request.CommandId.ToString("N")),
            "trailing" => input + "{}",
            _ => input + new string(' ', 65536)
        };
        Assert.Equal(2, await harness.Run(input));
        Assert.Equal(0, harness.Calls);
        Assert.DoesNotContain("test-token", harness.Output.ToString());
        Assert.DoesNotContain(OperationalTestData.Marker, harness.Output.ToString());
    }

    [Fact]
    public async Task Valid_read_is_sanitized_one_shot_and_options_immutable()
    {
        var harness = new RunnerHarness();
        harness.Options.MaximumInputBytes = 1;
        harness.Options.OperationTimeoutSeconds = 0;
        Assert.Equal(0, await harness.Run());
        Assert.Equal(1, harness.Calls);
        Assert.Equal(0, harness.PipePreflights);
        Assert.Equal(2, await harness.Run());
        Assert.Equal(1, harness.Calls);
        Assert.DoesNotContain("test-token", harness.Output.ToString());
        Assert.DoesNotContain(OperationalTestData.Marker, harness.Output.ToString());
    }

    [Theory]
    [InlineData("authorization", 3)]
    [InlineData("conflict", 4)]
    [InlineData("not-found", 4)]
    [InlineData("manual", 6)]
    [InlineData("pipe", 6)]
    [InlineData("unavailable", 5)]
    public async Task Failures_map_to_sanitized_nonzero_outcomes(string fault, int expected)
    {
        var harness = new RunnerHarness { Failure = fault };
        Assert.Equal(expected, await harness.Run());
        Assert.DoesNotContain("SENSITIVE-MESSAGE", harness.Output.ToString());
    }

    [Fact]
    public async Task Actual_anonymous_pipe_receives_one_secret_only_and_rejects_second_delivery()
    {
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        await using var sink = new ServiceClientSecretOutputSink();
        var handle = PipeHandles.ForSink(pipe);
        await sink.PreflightAsync(handle, CancellationToken.None);
        var secret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", secret, CancellationToken.None);
        using var reader = new StreamReader(pipe);
        var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var payload = JsonDocument.Parse(line!);
        Assert.True(payload.RootElement.GetProperty("Secret").GetString() == secret, "The OS pipe must contain the generated secret.");
        await Assert.ThrowsAnyAsync<Exception>(() => sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", secret, CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => sink.PreflightAsync(handle, CancellationToken.None));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("01")]
    [InlineData("C:\\output.txt")]
    [InlineData("-1")]
    [InlineData("not-a-handle")]
    public async Task Non_pipe_output_is_rejected(string handle)
    {
        await using var sink = new ServiceClientSecretOutputSink();
        await Assert.ThrowsAnyAsync<Exception>(() => sink.PreflightAsync(handle, CancellationToken.None));
    }

    [Fact]
    public async Task Closed_pipe_handle_is_rejected_without_opening_a_file()
    {
        string handle;
        using (var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable)) handle = pipe.GetClientHandleAsString();
        await using var sink = new ServiceClientSecretOutputSink();
        await Assert.ThrowsAnyAsync<Exception>(() => sink.PreflightAsync(handle, CancellationToken.None));
    }

    [Fact]
    public async Task Already_broken_pipe_peer_is_rejected_before_operation_can_mutate()
    {
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var handle = PipeHandles.ForSink(pipe);
        // Close only the reader endpoint: the inherited writer handle remains valid and pipe-typed.
        pipe.SafePipeHandle.Dispose();
        AssertStillAnOpenPipe(handle);
        await using var sink = new ServiceClientSecretOutputSink();
        await Assert.ThrowsAnyAsync<Exception>(() => sink.PreflightAsync(handle, CancellationToken.None));
    }

    // WP-AUTH-BL570-01: the same precondition on both platforms — the writer is still open and pipe-typed.
    private static void AssertStillAnOpenPipe(string handle)
    {
        var number = long.Parse(handle, System.Globalization.CultureInfo.InvariantCulture);
        if (OperatingSystem.IsWindows()) Assert.Equal(3u, GetFileType(new IntPtr(number)));
        else Assert.Equal(UnixPipeHandleInspector.TypeFifo, UnixPipeHandleInspector.Stat((int)number)?.Type);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr handle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [Fact]
    public async Task Actual_file_and_standard_stream_handles_are_not_secret_channels()
    {
        var path = Path.Combine(Path.GetTempPath(), "diten-pipe-negative-" + Guid.NewGuid().ToString("N"));
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        await using var fileSink = new ServiceClientSecretOutputSink();
        await Assert.ThrowsAnyAsync<Exception>(() => fileSink.PreflightAsync(file.SafeFileHandle.DangerousGetHandle().ToInt64().ToString(), CancellationToken.None));
        // Windows: the standard handles by value. Unix: fd 0–2 by number (the duplicates, by identity, are
        // ServiceClientSecretOutputSinkUnixTests).
        foreach (var standard in OperatingSystem.IsWindows()
                     ? new[] { -10, -11, -12 }.Select(stream => GetStdHandle(stream).ToInt64().ToString())
                     : ["0", "1", "2"])
        {
            await using var standardSink = new ServiceClientSecretOutputSink();
            await Assert.ThrowsAnyAsync<Exception>(() => standardSink.PreflightAsync(standard, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Broken_peer_runner_has_zero_service_calls()
    {
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var handle = PipeHandles.ForSink(pipe);
        pipe.SafePipeHandle.Dispose();
        AssertStillAnOpenPipe(handle);
        await using var sink = new ServiceClientSecretOutputSink();
        var harness = new RunnerHarness("rotate-credential", sink, handle);
        Assert.Equal(5, await harness.Run());
        Assert.Equal(0, harness.Calls);
    }

    // WP-AUTH-BL570-01 FIX1 R2(a): the whole fail-closed promise — when this OS cannot verify the pipe, the command
    // answers by name, exit 5, BEFORE the service runs: no credential is created or rotated.
    [Fact]
    public async Task An_unverifiable_secret_channel_platform_is_refused_by_name_before_any_credential_exists()
    {
        var harness = new RunnerHarness("rotate-credential", new UnsupportedPlatformSink(), "7");

        Assert.Equal(5, await harness.Run());
        Assert.Equal(0, harness.Calls);
        Assert.Contains("\"secret-output-unsupported-platform\"", harness.Output.ToString());
    }

    private sealed class UnsupportedPlatformSink : IServiceClientSecretOutputSink
    {
        public Task PreflightAsync(string handle, CancellationToken ct) =>
            throw new PlatformNotSupportedException(ServiceClientSecretOutputSink.UnsupportedPlatform);
        public Task DeliverOnceAsync(Guid id, string code, string secret, CancellationToken ct) =>
            throw new InvalidOperationException("must never be reached");
    }

    internal static string Envelope(ServiceClientOperationalProvisioningRequest request, string token = "test-token", string marker = OperationalTestData.Marker) =>
        JsonSerializer.Serialize(new { accessToken = token, operationalMarker = marker, request }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private sealed class RunnerHarness : IServiceClientOperationalActorAuthorizer, IServiceClientSecretOutputSink, IServiceClientOperationalProvisioningService
    {
        public ServiceClientOperationalProvisioningRequest Request { get; }
        public Diten.AuthService.Api.Configuration.ServiceClientOperationalProvisioningOptions Options { get; }
        public StringWriter Output { get; } = new();
        public int Calls { get; private set; }
        public int PipePreflights { get; private set; }
        public string? Failure { get; init; }
        private readonly ServiceClientOperationalProvisioningRunner _runner;
        public RunnerHarness(string operation = "read-identity", IServiceClientSecretOutputSink? sink = null, string? handle = null)
        {
            Request = OperationalTestData.Request(operation);
            if (handle is not null) Request = Request with { InheritedPipeHandle = handle };
            Options = OperationalTestData.Options(Request);
            _runner = new(Microsoft.Extensions.Options.Options.Create(Options), OperationalTestData.Eligibility(Request), this, sink ?? this, this);
        }
        public Task<int> Run(string? json = null, string[]? arguments = null) => _runner.RunAsync(arguments ?? [ServiceClientOperationalProvisioningRunner.Mode],
            new StringReader(json ?? Envelope(Request)), Output, CancellationToken.None);
        public Task<ServiceClientOperationalActor> AuthorizeAsync(string token, string marker, CancellationToken ct) =>
            Failure == "authorization" ? throw new UnauthorizedAccessException("SENSITIVE-MESSAGE") : Task.FromResult(OperationalTestData.Actor);
        public Task PreflightAsync(string handle, CancellationToken ct) { PipePreflights++; return Task.CompletedTask; }
        public Task DeliverOnceAsync(Guid id, string code, string secret, CancellationToken ct) => throw new NotSupportedException();
        public Task<ServiceClientOperationalProvisioningResult> ExecuteAsync(ServiceClientOperationalProvisioningRequest request, ServiceClientOperationalActor actor, CancellationToken ct)
        {
            Calls++;
            switch (Failure)
            {
                case "conflict": throw new ServiceClientOperationalConflictException("SENSITIVE-MESSAGE");
                case "not-found": throw new ServiceClientOperationalNotFoundException("SENSITIVE-MESSAGE");
                case "manual": throw new ServiceClientOperationalRecoveryRequiredException("SENSITIVE-MESSAGE");
                case "pipe": throw new ServiceClientOperationalSecretDeliveryException("SENSITIVE-MESSAGE");
                case "unavailable": throw new InvalidOperationException("SENSITIVE-MESSAGE");
            }
            return Task.FromResult(new ServiceClientOperationalProvisioningResult(request.Operation, request.ServiceClientIdentityId!.Value,
                request.TenantId, request.ClientCode!, request.ServiceName!, request.Audience!, 0, "v1", "fingerprint", false, "not-emitted"));
        }
    }
}
