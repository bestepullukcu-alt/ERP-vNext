using System.Net.Sockets;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.Notifications;
using Diten.Platform.Infrastructure.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

public sealed class NotificationsSmtpProviderTests
{
    [Fact]
    public async Task SmtpMessagingProvider_ShouldReturnFail_WhenCredentialSecretRefMissing()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId, credentialSecretRef: null));
        var factory = new RecordingSmtpClientFactory();
        var provider = CreateProvider(settings, factory);

        var result = await provider.SendEmailAsync(BuildRequest(tenantId));

        Assert.False(result.Accepted);
        Assert.Equal(MessagingProviderErrorCodes.ProviderConfigInvalid, result.ErrorCode);
        Assert.Equal(0, factory.CreatedCount);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldReturnFail_WhenSecretsProviderThrows()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var factory = new RecordingSmtpClientFactory();
        var provider = CreateProvider(settings, factory, secrets: new ThrowingSecretsProvider());

        var result = await provider.SendEmailAsync(BuildRequest(tenantId));

        Assert.False(result.Accepted);
        Assert.Equal(MessagingProviderErrorCodes.ProviderSecretUnresolved, result.ErrorCode);
        Assert.Equal(0, factory.CreatedCount);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldReturnFail_WhenSecretsProviderReturnsEmpty()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var factory = new RecordingSmtpClientFactory();
        var provider = CreateProvider(settings, factory, secrets: new InMemorySecretsProvider(string.Empty));

        var result = await provider.SendEmailAsync(BuildRequest(tenantId));

        Assert.False(result.Accepted);
        Assert.Equal(MessagingProviderErrorCodes.ProviderSecretUnresolved, result.ErrorCode);
        Assert.Equal(0, factory.CreatedCount);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldClassifyAuthFailure()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new FakeSmtpTransport
        {
            AuthenticateThrow = new MailKit.Security.AuthenticationException("auth-failed")
        };
        var factory = new RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        var result = await provider.SendEmailAsync(BuildRequest(tenantId));

        Assert.False(result.Accepted);
        Assert.Equal(MessagingProviderErrorCodes.ProviderAuthFailed, result.ErrorCode);
        Assert.DoesNotContain("auth-failed", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldClassifyTlsFailure()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new FakeSmtpTransport
        {
            ConnectThrow = new SslHandshakeException("tls-failed")
        };
        var factory = new RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        var result = await provider.SendEmailAsync(BuildRequest(tenantId));

        Assert.False(result.Accepted);
        Assert.Equal(MessagingProviderErrorCodes.ProviderTlsFailed, result.ErrorCode);
        Assert.DoesNotContain("tls-failed", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldClassifyTimeout()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new FakeSmtpTransport
        {
            SendBehavior = async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return "should-not-arrive";
            }
        };
        var factory = new RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory, options: new SmtpProviderOptions
        {
            SendTimeoutSeconds = 1
        });

        var result = await provider.SendEmailAsync(BuildRequest(tenantId));

        Assert.False(result.Accepted);
        Assert.Equal(MessagingProviderErrorCodes.ProviderTimeout, result.ErrorCode);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldClassifyConnectivityFailure()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new FakeSmtpTransport
        {
            ConnectThrow = new SocketException()
        };
        var factory = new RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        var result = await provider.SendEmailAsync(BuildRequest(tenantId));

        Assert.False(result.Accepted);
        Assert.Equal(MessagingProviderErrorCodes.ProviderConnectivityFailed, result.ErrorCode);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldClassifySmtpRejectionWithStableCode()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new FakeSmtpTransport
        {
            SendThrow = new SmtpCommandException(
                SmtpErrorCode.MessageNotAccepted,
                SmtpStatusCode.MailboxUnavailable,
                "User unknown")
        };
        var factory = new RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        var result = await provider.SendEmailAsync(BuildRequest(tenantId));

        Assert.False(result.Accepted);
        Assert.Equal($"{MessagingProviderErrorCodes.ProviderRejected}:550", result.ErrorCode);
        Assert.DoesNotContain("User unknown", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldRejectExcessiveRecipientCount_WithoutInvokingTransport()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var factory = new RecordingSmtpClientFactory();
        var provider = CreateProvider(settings, factory, options: new SmtpProviderOptions
        {
            MaxRecipientsPerMessage = 2
        });

        var to = new EmailRecipientDto[]
        {
            new("a@example.com", null),
            new("b@example.com", null),
            new("c@example.com", null)
        };

        var result = await provider.SendEmailAsync(BuildRequest(tenantId, to: to));

        Assert.False(result.Accepted);
        Assert.Equal(MessagingProviderErrorCodes.ProviderRejectedRecipientLimit, result.ErrorCode);
        Assert.Equal(0, factory.CreatedCount);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldReturnAccepted_WithProviderMessageId_OnSuccess()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("OK 250 queued-id")
        };
        var factory = new RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        var result = await provider.SendEmailAsync(BuildRequest(tenantId));

        Assert.True(result.Accepted);
        Assert.Equal("OK 250 queued-id", result.ProviderMessageId);
        Assert.Equal(1, factory.CreatedCount);
        Assert.True(transport.ConnectCount > 0);
        Assert.True(transport.AuthenticateCount > 0);
        Assert.True(transport.DisconnectCount > 0);
    }

    [Fact]
    public async Task SmtpMessagingProvider_ShouldNeverLogRawSecret_OrFullBody_OrFullRecipientList()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("provider-msg-id")
        };
        var factory = new RecordingSmtpClientFactory(transport);
        var logger = new CapturingLogger<SmtpMessagingProvider>();
        var secretValue = "super-secret-password-shhh";
        var fullBody = "<p>FULL_SENSITIVE_BODY_CONTENT</p>";
        var provider = CreateProvider(
            settings,
            factory,
            secrets: new InMemorySecretsProvider(secretValue),
            logger: logger);

        var request = BuildRequest(
            tenantId,
            subject: "VERY_LONG_SUBJECT_WITH_ALOT_OF_SENSITIVE_DATA",
            to: [new("to1@example.com", null), new("to2@example.com", null)],
            bodyHtmlPreview: fullBody);

        var result = await provider.SendEmailAsync(request);

        Assert.True(result.Accepted);
        foreach (var entry in logger.Entries)
        {
            var rendered = entry.Render();
            Assert.DoesNotContain(secretValue, rendered, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FULL_SENSITIVE_BODY_CONTENT", rendered, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("to1@example.com", rendered, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("to2@example.com", rendered, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SmtpMessagingProvider_ShouldNeverReferenceRabbitMqOrMassTransitOrHangfire()
    {
        var assembly = typeof(SmtpMessagingProvider).Assembly;
        var folderTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Diten.Platform.Infrastructure.Services.Notifications", StringComparison.Ordinal) == true
                        && t != typeof(FakeMessagingProvider)
                        && t != typeof(MessagingProviderResolver));

        foreach (var type in folderTypes)
        {
            var typeName = type.FullName ?? type.Name;

            var members = type.GetMembers(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.DeclaredOnly);

            foreach (var member in members)
            {
                if (member is System.Reflection.PropertyInfo property)
                {
                    AssertNotForbidden(property.PropertyType.FullName, typeName);
                }
                else if (member is System.Reflection.FieldInfo field)
                {
                    AssertNotForbidden(field.FieldType.FullName, typeName);
                }
                else if (member is System.Reflection.ConstructorInfo ctor)
                {
                    foreach (var parameter in ctor.GetParameters())
                    {
                        AssertNotForbidden(parameter.ParameterType.FullName, typeName);
                    }
                }
                else if (member is System.Reflection.MethodInfo method)
                {
                    AssertNotForbidden(method.ReturnType.FullName, typeName);
                    foreach (var parameter in method.GetParameters())
                    {
                        AssertNotForbidden(parameter.ParameterType.FullName, typeName);
                    }
                }
            }

            Assert.False(typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(type),
                $"{typeName} must not implement IHostedService (background loops are owned by MOD-0026/MOD-0027).");
        }
    }

    [Fact]
    public void SmtpProviderOptions_ShouldThrowAtStartup_WhenAllowInsecureTlsInDevelopmentTrueInProduction()
    {
        var validator = new SmtpProviderOptionsValidator(new TestHostEnvironment("Production"));
        var options = new SmtpProviderOptions
        {
            AllowInsecureTlsInDevelopment = true
        };

        var result = validator.Validate(SmtpProviderOptions.SectionName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("AllowInsecureTlsInDevelopment", StringComparison.Ordinal));
    }

    [Fact]
    public void SmtpProviderOptions_ShouldThrowAtStartup_WhenSendTimeoutOutOfRange()
    {
        var validator = new SmtpProviderOptionsValidator(new TestHostEnvironment("Development"));

        var tooLow = validator.Validate(null, new SmtpProviderOptions { SendTimeoutSeconds = 0 });
        var tooHigh = validator.Validate(null, new SmtpProviderOptions { SendTimeoutSeconds = 1000 });

        Assert.True(tooLow.Failed);
        Assert.True(tooHigh.Failed);
    }

    private static void AssertNotForbidden(string? typeFullName, string ownerTypeName)
    {
        if (string.IsNullOrEmpty(typeFullName))
        {
            return;
        }

        foreach (var forbidden in new[] { "RabbitMQ.Client", "MassTransit", "Hangfire" })
        {
            Assert.False(
                typeFullName.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"{ownerTypeName} references forbidden namespace '{forbidden}' via member type '{typeFullName}'.");
        }
    }

    private static MessagingProviderEmailRequest BuildRequest(
        Guid tenantId,
        string subject = "Welcome",
        IReadOnlyList<EmailRecipientDto>? to = null,
        string? bodyHtmlPreview = "<p>Hello</p>") =>
        new(
            DispatchId: Guid.NewGuid(),
            TenantId: tenantId,
            CorrelationId: "corr-test",
            Subject: subject,
            To: to ?? [new EmailRecipientDto("user@example.com", "User")],
            Cc: [],
            Bcc: [],
            BodyHtmlPreview: bodyHtmlPreview,
            BodyTextPreview: "Hello");

    private static TenantMessagingSettings CreateSettings(
        Guid tenantId,
        string? credentialSecretRef = "secret:platform:smtp:default",
        bool useSsl = true,
        string host = "smtp.example.test",
        int port = 587,
        bool isPlatformDefault = false) =>
        new()
        {
            TenantId = isPlatformDefault ? null : tenantId,
            IsPlatformDefault = isPlatformDefault,
            ProviderCode = MessagingProviderCode.Smtp,
            SenderEmail = "sender@example.com",
            SenderName = "Sender",
            Host = host,
            Port = port,
            UseSsl = useSsl,
            CredentialSecretRef = credentialSecretRef,
            IsEnabled = true,
            FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault
        };

    private static InMemoryTenantMessagingSettingsRepository CreateRepository(params TenantMessagingSettings[] items)
    {
        var repo = new InMemoryTenantMessagingSettingsRepository();
        foreach (var item in items)
        {
            repo.CreateAsync(item).GetAwaiter().GetResult();
        }

        return repo;
    }

    private static SmtpMessagingProvider CreateProvider(
        ITenantMessagingSettingsRepository repository,
        ISmtpClientFactory factory,
        ISecretsProvider? secrets = null,
        SmtpProviderOptions? options = null,
        ILogger<SmtpMessagingProvider>? logger = null,
        IHostEnvironment? environment = null)
    {
        secrets ??= new InMemorySecretsProvider("resolved-password");
        options ??= new SmtpProviderOptions();
        logger ??= NullLogger<SmtpMessagingProvider>.Instance;
        environment ??= new TestHostEnvironment("Development");

        var monitor = new StaticOptionsMonitor<SmtpProviderOptions>(options);
        var secretResolver = new SecretReferenceResolver(secrets);
        return new SmtpMessagingProvider(monitor, repository, factory, secretResolver, environment, logger);
    }

    internal sealed class InMemoryTenantMessagingSettingsRepository : ITenantMessagingSettingsRepository
    {
        private readonly List<TenantMessagingSettings> _items = [];

        public Task<TenantMessagingSettings> CreateAsync(TenantMessagingSettings settings, CancellationToken ct = default)
        {
            _items.Add(settings);
            return Task.FromResult(settings);
        }

        public Task<TenantMessagingSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && !x.IsPlatformDefault && x.TenantId == tenantId));

        public Task<TenantMessagingSettings?> GetPlatformDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && x.IsPlatformDefault && x.TenantId is null));

        public Task<TenantMessagingSettings?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && x.TenantId == tenantId && x.Id == id));

        public Task<TenantMessagingSettings?> GetPlatformDefaultByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => !x.IsDeleted && x.IsPlatformDefault && x.Id == id));

        public Task UpdateAsync(TenantMessagingSettings settings, CancellationToken ct = default) => Task.CompletedTask;

        public Task SoftDeleteTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;

        public Task SoftDeletePlatformDefaultAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class RecordingSmtpClientFactory : ISmtpClientFactory
    {
        private readonly FakeSmtpTransport _transport;

        public RecordingSmtpClientFactory() : this(new FakeSmtpTransport()) { }

        public RecordingSmtpClientFactory(FakeSmtpTransport transport)
        {
            _transport = transport;
        }

        public int CreatedCount { get; private set; }

        public ISmtpTransport Create()
        {
            CreatedCount++;
            return _transport;
        }
    }

    internal sealed class FakeSmtpTransport : ISmtpTransport
    {
        public Exception? ConnectThrow { get; set; }
        public Exception? AuthenticateThrow { get; set; }
        public Exception? SendThrow { get; set; }
        public Func<MimeMessage, CancellationToken, Task<string>>? SendBehavior { get; set; }

        public int ConnectCount { get; private set; }
        public int AuthenticateCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public int Timeout { get; set; }
        public string? LastAuthUserName { get; private set; }
        public MimeMessage? LastSentMessage { get; private set; }

        public Task ConnectAsync(string host, int port, SecureSocketOptions socketOptions, CancellationToken ct)
        {
            ConnectCount++;
            if (ConnectThrow is not null) throw ConnectThrow;
            return Task.CompletedTask;
        }

        public Task AuthenticateAsync(string userName, string password, CancellationToken ct)
        {
            AuthenticateCount++;
            LastAuthUserName = userName;
            if (AuthenticateThrow is not null) throw AuthenticateThrow;
            return Task.CompletedTask;
        }

        public Task<string> SendAsync(MimeMessage message, CancellationToken ct)
        {
            LastSentMessage = message;
            if (SendThrow is not null) throw SendThrow;
            return SendBehavior?.Invoke(message, ct) ?? Task.FromResult(string.Empty);
        }

        public Task DisconnectAsync(bool quit, CancellationToken ct)
        {
            DisconnectCount++;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    internal sealed class InMemorySecretsProvider : ISecretsProvider
    {
        private readonly string _value;

        public InMemorySecretsProvider(string value)
        {
            _value = value;
        }

        public Task<string> GetSecretAsync(string key, CancellationToken ct) => Task.FromResult(_value);

        public Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    internal sealed class ThrowingSecretsProvider : ISecretsProvider
    {
        public Task<string> GetSecretAsync(string key, CancellationToken ct) =>
            throw new InvalidOperationException("secret store unavailable");

        public Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct) =>
            throw new InvalidOperationException("secret store unavailable");
    }

    internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) => CurrentValue = value;

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    internal sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Diten.Platform.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    internal sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }

        public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception)
        {
            public string Render() => $"{Level} {Message} {Exception}";
        }
    }
}
