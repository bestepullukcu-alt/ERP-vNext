using System.Text.Json;
using System.IO.Pipes;
using System.Globalization;
using System.Runtime.InteropServices;
using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceClientOperationalProvisioningRunnerTests
{
    private static readonly Guid CommandId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Exact_mode_and_strict_envelope_are_required()
    {
        var runner = Build();
        var output = new StringWriter();

        Assert.Equal(2, await runner.RunAsync([], new StringReader("{}"), output, default));
        Assert.Equal(2, await runner.RunAsync(
            [ServiceClientOperationalProvisioningRunner.Mode],
            new StringReader("{\"accessToken\":\"x\",\"operationalMarker\":\"y\",\"request\":{},\"extra\":1}"),
            output,
            default));
    }

    [Fact]
    public async Task Successful_create_preflights_pipe_and_never_writes_secret_to_stdout()
    {
        var sink = new FakeSink();
        var runner = Build(sink);
        var output = new StringWriter();

        var exit = await runner.RunAsync(
            [ServiceClientOperationalProvisioningRunner.Mode],
            new StringReader(Envelope()),
            output,
            default);

        Assert.Equal(0, exit);
        Assert.Equal("1234", sink.PreflightHandle);
        Assert.DoesNotContain("raw-secret", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("issued-once", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorization_denial_is_sanitized()
    {
        var runner = Build(authorizer: new DenyingAuthorizer());
        var output = new StringWriter();

        var exit = await runner.RunAsync(
            [ServiceClientOperationalProvisioningRunner.Mode],
            new StringReader(Envelope()),
            output,
            default);

        Assert.Equal(3, exit);
        Assert.Equal("{\"status\":\"failed\",\"code\":\"authorization-denied\"}" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public async Task Token_larger_than_16KiB_and_within_32KiB_reaches_authorizer()
    {
        var authorizer = new RecordingAuthorizer();
        var runner = Build(authorizer: authorizer);
        var token = new string('t', 20_995);

        var exit = await runner.RunAsync(
            [ServiceClientOperationalProvisioningRunner.Mode],
            new StringReader(Envelope(token)),
            new StringWriter(),
            default);

        Assert.Equal(0, exit);
        Assert.Equal(token, authorizer.AccessToken);
    }

    [Fact]
    public async Task Token_larger_than_32KiB_is_rejected_before_authorization_or_mutation()
    {
        var authorizer = new RecordingAuthorizer();
        var service = new RecordingService();
        var runner = Build(authorizer: authorizer, service: service);

        var exit = await runner.RunAsync(
            [ServiceClientOperationalProvisioningRunner.Mode],
            new StringReader(Envelope(new string('t', (32 * 1024) + 1))),
            new StringWriter(),
            default);

        Assert.Equal(2, exit);
        Assert.Null(authorizer.AccessToken);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task Completed_mutation_with_lost_secret_delivery_maps_to_rotation_required_exit()
    {
        var runner = Build(service: new RecoveryRequiredService());
        var output = new StringWriter();

        var exit = await runner.RunAsync(
            [ServiceClientOperationalProvisioningRunner.Mode],
            new StringReader(Envelope()),
            output,
            default);

        Assert.Equal(6, exit);
        Assert.Equal(
            "{\"status\":\"recovery-required\",\"code\":\"rotation-required\"}" + Environment.NewLine,
            output.ToString());
    }

    [Fact]
    public async Task Concrete_sink_uses_only_inherited_anonymous_pipe_and_delivers_once()
    {
        await using var server = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        await using var sink = new ServiceClientSecretOutputSink();
        await sink.PreflightAsync(server.GetClientHandleAsString(), default);

        var identityId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await sink.DeliverOnceAsync(identityId, "MDM-WORKFLOW-LOCAL", "raw-secret", default);
        var buffer = new byte[4096];
        var count = await server.ReadAsync(buffer);
        var payload = System.Text.Encoding.UTF8.GetString(buffer, 0, count);

        Assert.Contains(identityId.ToString("D"), payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw-secret", payload, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sink.DeliverOnceAsync(identityId, "MDM-WORKFLOW-LOCAL", "second-secret", default));
    }

    [Fact]
    public async Task Concrete_sink_rejects_file_or_named_path()
    {
        await using var sink = new ServiceClientSecretOutputSink();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sink.PreflightAsync("C:\\temp\\secret.txt", default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Concrete_sink_rejects_stdin_stdout_and_stderr_handles(int descriptor)
    {
        await using var sink = new ServiceClientSecretOutputSink();
        var handle = OperatingSystem.IsWindows()
            ? GetStdHandle(-10 - descriptor).ToInt64().ToString(CultureInfo.InvariantCulture)
            : descriptor.ToString(CultureInfo.InvariantCulture);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sink.PreflightAsync(handle, default));
    }

    [Fact]
    public void Operational_registration_is_not_hosted_and_normal_startup_args_do_not_select_runner()
    {
        var services = new ServiceCollection();
        ServiceClientOperationalProvisioningRunner.AddOperationalProvisioning(
            services,
            new ConfigurationBuilder().Build());

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ServiceClientOperationalProvisioningRunner)
                                                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.False(ServiceClientOperationalProvisioningRunner.IsProcessInvocationRequested([]));
        Assert.False(ServiceClientOperationalProvisioningRunner.IsProcessInvocationRequested(["--urls", "http://localhost:5056"]));
        Assert.True(ServiceClientOperationalProvisioningRunner.IsProcessInvocationRequested(
            [ServiceClientOperationalProvisioningRunner.Mode]));
    }

    private static ServiceClientOperationalProvisioningRunner Build(
        FakeSink? sink = null,
        IServiceClientOperationalActorAuthorizer? authorizer = null,
        IServiceClientOperationalProvisioningService? service = null) => new(
        Options.Create(new ServiceClientOperationalProvisioningOptions
        {
            MaximumInputBytes = 64 * 1024,
            OperationTimeoutSeconds = 30
        }),
        new AllowEligibility(),
        authorizer ?? new AllowAuthorizer(),
        sink ?? new FakeSink(),
        service ?? new FakeService());

    private static string Envelope(string accessToken = "token-not-echoed") => JsonSerializer.Serialize(new
    {
        accessToken,
        operationalMarker = "marker-not-echoed",
        request = new
        {
            operation = ServiceClientOperationalOperations.CreateIdentity,
            commandId = CommandId.ToString("D"),
            expectedOperationalVersion = 0,
            serviceClientIdentityId = (string?)null,
            tenantId = (string?)null,
            clientCode = "MDM-WORKFLOW-LOCAL",
            serviceName = "Diten.MDM",
            audience = "TRUSTED_WORKFLOW_CONSUMER",
            inheritedPipeHandle = "1234"
        }
    });

    private sealed class AllowEligibility : IServiceClientOperationalProvisioningEligibility
    {
        public void EnsureEligible(ServiceClientOperationalProvisioningRequest request) { }
    }

    private sealed class AllowAuthorizer : IServiceClientOperationalActorAuthorizer
    {
        public Task<ServiceClientOperationalActor> AuthorizeAsync(string accessToken, string operationalMarker, CancellationToken cancellationToken)
            => Task.FromResult(new ServiceClientOperationalActor(Guid.NewGuid(), Guid.NewGuid(), "platform_admin"));
    }

    private sealed class DenyingAuthorizer : IServiceClientOperationalActorAuthorizer
    {
        public Task<ServiceClientOperationalActor> AuthorizeAsync(string accessToken, string operationalMarker, CancellationToken cancellationToken)
            => throw new UnauthorizedAccessException("sensitive detail");
    }

    private sealed class RecordingAuthorizer : IServiceClientOperationalActorAuthorizer
    {
        public string? AccessToken { get; private set; }

        public Task<ServiceClientOperationalActor> AuthorizeAsync(
            string accessToken,
            string operationalMarker,
            CancellationToken cancellationToken)
        {
            AccessToken = accessToken;
            return Task.FromResult(new ServiceClientOperationalActor(Guid.NewGuid(), Guid.NewGuid(), "platform_admin"));
        }
    }

    private sealed class FakeSink : IServiceClientSecretOutputSink
    {
        public string? PreflightHandle { get; private set; }

        public Task PreflightAsync(string inheritedPipeHandle, CancellationToken cancellationToken)
        {
            PreflightHandle = inheritedPipeHandle;
            return Task.CompletedTask;
        }

        public Task DeliverOnceAsync(Guid serviceClientIdentityId, string clientCode, string rawSecret, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeService : IServiceClientOperationalProvisioningService
    {
        public Task<ServiceClientOperationalProvisioningResult> ExecuteAsync(
            ServiceClientOperationalProvisioningRequest request,
            ServiceClientOperationalActor actor,
            CancellationToken cancellationToken) => Task.FromResult(new ServiceClientOperationalProvisioningResult(
                request.Operation,
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                null,
                request.ClientCode!,
                request.ServiceName!,
                request.Audience!,
                1,
                new string('a', 64),
                false,
                "issued-once"));
    }

    private sealed class RecoveryRequiredService : IServiceClientOperationalProvisioningService
    {
        public Task<ServiceClientOperationalProvisioningResult> ExecuteAsync(
            ServiceClientOperationalProvisioningRequest request,
            ServiceClientOperationalActor actor,
            CancellationToken cancellationToken) =>
            throw new ServiceClientOperationalSecretDeliveryException(
                "secret-value-must-not-be-emitted");
    }

    private sealed class RecordingService : IServiceClientOperationalProvisioningService
    {
        public bool WasCalled { get; private set; }

        public Task<ServiceClientOperationalProvisioningResult> ExecuteAsync(
            ServiceClientOperationalProvisioningRequest request,
            ServiceClientOperationalActor actor,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("Mutation must not be reached.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);
}
