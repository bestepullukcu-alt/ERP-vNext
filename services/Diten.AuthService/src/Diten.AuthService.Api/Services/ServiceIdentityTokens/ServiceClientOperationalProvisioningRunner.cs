using System.Text;
using System.Text.Json;
using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

public sealed class ServiceClientOperationalProvisioningRunner
{
    public const string Mode = "--service-client-operational-run";
    internal const int MaximumAccessTokenCharacters = 32 * 1024;

    public static bool IsProcessInvocationRequested(IReadOnlyList<string> arguments)
        => arguments.Any(argument => string.Equals(argument, Mode, StringComparison.Ordinal));

    public static IServiceCollection AddOperationalProvisioning(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ServiceClientOperationalProvisioningOptions>(
            configuration.GetSection(ServiceClientOperationalProvisioningOptions.SectionName));
        services.AddScoped<IServiceClientOperationalProvisioningEligibility, DevelopmentServiceClientOperationalProvisioningEligibility>();
        services.AddScoped<IServiceClientOperationalActorAuthorizer, ServiceClientOperationalActorAuthorizer>();
        services.AddScoped<IServiceClientSecretOutputSink, ServiceClientSecretOutputSink>();
        services.AddScoped<ServiceClientOperationalProvisioningRunner>();
        return services;
    }

    private readonly ServiceClientOperationalProvisioningOptions _options;
    private readonly IServiceClientOperationalProvisioningEligibility _eligibility;
    private readonly IServiceClientOperationalActorAuthorizer _actorAuthorizer;
    private readonly IServiceClientSecretOutputSink _secretOutputSink;
    private readonly IServiceClientOperationalProvisioningService _provisioningService;

    public ServiceClientOperationalProvisioningRunner(
        IOptions<ServiceClientOperationalProvisioningOptions> options,
        IServiceClientOperationalProvisioningEligibility eligibility,
        IServiceClientOperationalActorAuthorizer actorAuthorizer,
        IServiceClientSecretOutputSink secretOutputSink,
        IServiceClientOperationalProvisioningService provisioningService)
    {
        _options = options.Value;
        _eligibility = eligibility;
        _actorAuthorizer = actorAuthorizer;
        _secretOutputSink = secretOutputSink;
        _provisioningService = provisioningService;
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (arguments.Count != 1 || !string.Equals(arguments[0], Mode, StringComparison.Ordinal))
        {
            await WriteFailureAsync(output, "contract", cancellationToken);
            return 2;
        }

        try
        {
            var envelope = await ParseEnvelopeAsync(input, _options.MaximumInputBytes, cancellationToken);
            _eligibility.EnsureEligible(envelope.Request);

            var actor = await _actorAuthorizer.AuthorizeAsync(
                envelope.AccessToken,
                envelope.OperationalMarker,
                cancellationToken);

            if (ServiceClientOperationalOperations.EmitsSecret(envelope.Request.Operation))
            {
                await _secretOutputSink.PreflightAsync(
                    envelope.Request.InheritedPipeHandle!,
                    cancellationToken);
            }

            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Clamp(_options.OperationTimeoutSeconds, 1, 120)));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            var result = await _provisioningService.ExecuteAsync(envelope.Request, actor, linked.Token);
            await output.WriteLineAsync(JsonSerializer.Serialize(new
            {
                status = "completed",
                result.Operation,
                result.ServiceClientIdentityId,
                result.TenantId,
                result.ClientCode,
                result.ServiceName,
                result.Audience,
                result.OperationalVersion,
                result.CommandFingerprint,
                result.IsReplay,
                result.SecretDisposition
            }));
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await WriteFailureAsync(output, "cancelled", CancellationToken.None);
            return 130;
        }
        catch (OperationCanceledException)
        {
            await WriteFailureAsync(output, "unavailable", CancellationToken.None);
            return 5;
        }
        catch (UnauthorizedAccessException)
        {
            await WriteFailureAsync(output, "authorization-denied", cancellationToken);
            return 3;
        }
        catch (ServiceClientOperationalContractException)
        {
            await WriteFailureAsync(output, "contract", cancellationToken);
            return 2;
        }
        catch (ServiceClientOperationalConflictException)
        {
            await WriteFailureAsync(output, "conflict", cancellationToken);
            return 4;
        }
        catch (ServiceClientOperationalNotFoundException)
        {
            await WriteFailureAsync(output, "not-found", cancellationToken);
            return 4;
        }
        catch (ServiceClientOperationalSecretDeliveryException)
        {
            await WriteOutcomeAsync(output, "recovery-required", "rotation-required", cancellationToken);
            return 6;
        }
        catch (ServiceClientOperationalRecoveryRequiredException)
        {
            await WriteFailureAsync(output, "recovery-required", cancellationToken);
            return 6;
        }
        catch (InvalidOperationException)
        {
            await WriteFailureAsync(output, "configuration", cancellationToken);
            return 2;
        }
        catch
        {
            await WriteFailureAsync(output, "unavailable", cancellationToken);
            return 5;
        }
    }

    private static async Task<OperationalEnvelope> ParseEnvelopeAsync(
        TextReader input,
        int maximumInputBytes,
        CancellationToken cancellationToken)
    {
        if (maximumInputBytes is < 1024 or > 64 * 1024)
        {
            throw new ServiceClientOperationalContractException("Operational input budget is invalid.");
        }

        var buffer = new char[2048];
        var builder = new StringBuilder();
        while (true)
        {
            var count = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
            {
                break;
            }

            builder.Append(buffer, 0, count);
            if (builder.Length > maximumInputBytes
                || Encoding.UTF8.GetByteCount(builder.ToString()) > maximumInputBytes)
            {
                throw new ServiceClientOperationalContractException("Operational input exceeds its budget.");
            }
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(builder.ToString(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
        }
        catch (JsonException)
        {
            throw new ServiceClientOperationalContractException("Operational input is malformed.");
        }

        using (document)
        {
            var root = document.RootElement;
            EnsureExactObject(root, ["accessToken", "operationalMarker", "request"]);
            var accessToken = ReadRequiredString(root, "accessToken", MaximumAccessTokenCharacters);
            var marker = ReadRequiredString(root, "operationalMarker", 1024);
            var requestElement = root.GetProperty("request");
            EnsureExactObject(requestElement,
            [
                "operation", "commandId", "expectedOperationalVersion", "serviceClientIdentityId",
                "tenantId", "clientCode", "serviceName", "audience", "inheritedPipeHandle"
            ]);

            var request = new ServiceClientOperationalProvisioningRequest(
                ReadRequiredString(requestElement, "operation", 64),
                ReadRequiredGuid(requestElement, "commandId"),
                ReadRequiredInt64(requestElement, "expectedOperationalVersion"),
                ReadOptionalGuid(requestElement, "serviceClientIdentityId"),
                ReadOptionalGuid(requestElement, "tenantId"),
                ReadOptionalString(requestElement, "clientCode", 128),
                ReadOptionalString(requestElement, "serviceName", 128),
                ReadOptionalString(requestElement, "audience", 128),
                ReadOptionalString(requestElement, "inheritedPipeHandle", 128));
            return new OperationalEnvelope(accessToken, marker, request);
        }
    }

    private static void EnsureExactObject(JsonElement element, IReadOnlyCollection<string> expectedProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ServiceClientOperationalContractException("Operational input is malformed.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !expectedProperties.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new ServiceClientOperationalContractException("Operational input is malformed.");
            }
        }

        if (seen.Count != expectedProperties.Count
            || expectedProperties.Any(property => !seen.Contains(property)))
        {
            throw new ServiceClientOperationalContractException("Operational input is malformed.");
        }
    }

    private static string ReadRequiredString(JsonElement element, string name, int maximumLength)
        => ReadOptionalString(element, name, maximumLength)
           ?? throw new ServiceClientOperationalContractException("Operational input is malformed.");

    private static string? ReadOptionalString(JsonElement element, string name, int maximumLength)
    {
        var value = element.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ServiceClientOperationalContractException("Operational input is malformed.");
        }

        var text = value.GetString();
        if (text is null || text.Length > maximumLength)
        {
            throw new ServiceClientOperationalContractException("Operational input is malformed.");
        }

        return text;
    }

    private static Guid ReadRequiredGuid(JsonElement element, string name)
    {
        var text = ReadRequiredString(element, name, 36);
        return Guid.TryParseExact(text, "D", out var value) && value != Guid.Empty
            ? value
            : throw new ServiceClientOperationalContractException("Operational input is malformed.");
    }

    private static Guid? ReadOptionalGuid(JsonElement element, string name)
    {
        var text = ReadOptionalString(element, name, 36);
        if (text is null)
        {
            return null;
        }

        return Guid.TryParseExact(text, "D", out var value) && value != Guid.Empty
            ? value
            : throw new ServiceClientOperationalContractException("Operational input is malformed.");
    }

    private static long ReadRequiredInt64(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number >= 0
            ? number
            : throw new ServiceClientOperationalContractException("Operational input is malformed.");
    }

    private static Task WriteFailureAsync(TextWriter output, string code, CancellationToken cancellationToken)
        => WriteOutcomeAsync(output, "failed", code, cancellationToken);

    private static Task WriteOutcomeAsync(
        TextWriter output,
        string status,
        string code,
        CancellationToken cancellationToken)
        => output.WriteLineAsync(JsonSerializer.Serialize(new { status, code }).AsMemory(), cancellationToken);

    private sealed record OperationalEnvelope(
        string AccessToken,
        string OperationalMarker,
        ServiceClientOperationalProvisioningRequest Request);
}
