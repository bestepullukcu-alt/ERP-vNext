using System.Text;
using System.Text.Json;
using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

public sealed class ServiceClientOperationalProvisioningRunner
{
    public const string Mode = "--service-client-operational-run";
    public const int MaximumAccessTokenCharacters = 32 * 1024;
    private readonly ServiceClientOperationalProvisioningOptions _options;
    private readonly IServiceClientOperationalProvisioningEligibility _eligibility;
    private readonly IServiceClientOperationalActorAuthorizer _actor;
    private readonly IServiceClientSecretOutputSink _sink;
    private readonly IServiceClientOperationalProvisioningService _service;
    private int _invoked;

    public ServiceClientOperationalProvisioningRunner(IOptions<ServiceClientOperationalProvisioningOptions> options,
        IServiceClientOperationalProvisioningEligibility eligibility, IServiceClientOperationalActorAuthorizer actor,
        IServiceClientSecretOutputSink sink, IServiceClientOperationalProvisioningService service)
    {
        _options = options.Value.Snapshot();
        _eligibility = eligibility;
        _actor = actor;
        _sink = sink;
        _service = service;
    }

    // Capture misspellings/case variants of this command family so they cannot fall through to normal startup.
    public static bool IsProcessInvocationRequested(IReadOnlyList<string> arguments) =>
        arguments.Any(argument => argument.StartsWith("--service-client", StringComparison.OrdinalIgnoreCase));

    public static bool IsExactInvocation(IReadOnlyList<string> arguments) =>
        arguments.Count == 1 && string.Equals(arguments[0], Mode, StringComparison.Ordinal);

    public async Task<int> RunAsync(IReadOnlyList<string> arguments, TextReader input, TextWriter output,
        CancellationToken cancellationToken)
    {
        if (!IsExactInvocation(arguments) || Interlocked.Exchange(ref _invoked, 1) != 0)
            return await FailAsync(output, "contract", 2);
        if (_options.OperationTimeoutSeconds is < 1 or > 120)
            return await FailAsync(output, "configuration", 2);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(_options.OperationTimeoutSeconds));
        try
        {
            var envelope = await ParseEnvelopeAsync(input, _options.MaximumInputBytes, budget.Token);
            _eligibility.EnsureEligible(envelope.Request);
            var actor = await _actor.AuthorizeAsync(envelope.AccessToken, envelope.OperationalMarker, budget.Token);
            if (ServiceClientOperationalOperations.EmitsSecret(envelope.Request.Operation))
                await _sink.PreflightAsync(envelope.Request.InheritedPipeHandle!, budget.Token);
            var result = await _service.ExecuteAsync(envelope.Request, actor, budget.Token);
            // This result has no credential material. A sink write is not a consumer-installation receipt.
            await output.WriteLineAsync(JsonSerializer.Serialize(new { status = "completed", result }).AsMemory(), budget.Token);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return await FailAsync(output, "cancelled", 130); }
        catch (OperationCanceledException)
        { return await FailAsync(output, "timeout", 5); }
        catch (UnauthorizedAccessException)
        { return await FailAsync(output, "authorization-denied", 3); }
        catch (ServiceClientOperationalContractException)
        { return await FailAsync(output, "contract", 2); }
        catch (ServiceClientOperationalConflictException)
        { return await FailAsync(output, "conflict", 4); }
        catch (ServiceClientOperationalNotFoundException)
        { return await FailAsync(output, "not-found", 4); }
        catch (ServiceClientOperationalSecretDeliveryException)
        { return await FailAsync(output, "credential-mutated-secret-delivery-uncertain", 6); }
        catch (ServiceClientOperationalRecoveryRequiredException)
        { return await FailAsync(output, "manual-reconciliation-required", 6); }
        catch (InvalidOperationException)
        { return await FailAsync(output, "configuration-or-storage-unavailable", 5); }
        catch
        { return await FailAsync(output, "unavailable", 5); }
    }

    private static async Task<OperationalEnvelope> ParseEnvelopeAsync(TextReader input, int maximumInputBytes,
        CancellationToken cancellationToken)
    {
        if (maximumInputBytes is < 1024 or > 64 * 1024)
            throw new ServiceClientOperationalContractException("Operational input budget is invalid.");
        var buffer = new char[2048];
        var builder = new StringBuilder();
        try
        {
            while (true)
            {
                var count = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (count == 0) break;
                builder.Append(buffer, 0, count);
                if (builder.Length > maximumInputBytes || Encoding.UTF8.GetByteCount(builder.ToString()) > maximumInputBytes)
                    throw new ServiceClientOperationalContractException("Operational input exceeds its budget.");
            }
            using var document = JsonDocument.Parse(builder.ToString(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 8
            });
            var root = document.RootElement;
            ExactObject(root, ["accessToken", "operationalMarker", "request"]);
            var request = root.GetProperty("request");
            ExactObject(request, ["operation", "commandId", "expectedOperationalVersion", "serviceClientIdentityId",
                "tenantId", "clientCode", "serviceName", "audience", "inheritedPipeHandle", "expectedCredentialVersion"]);
            return new OperationalEnvelope(ReadText(root, "accessToken", MaximumAccessTokenCharacters, false)!,
                ReadText(root, "operationalMarker", 1024, false)!,
                new ServiceClientOperationalProvisioningRequest(ReadText(request, "operation", 64, false)!,
                    ReadGuid(request, "commandId", false)!.Value, ReadVersion(request),
                    ReadGuid(request, "serviceClientIdentityId", false), ReadGuid(request, "tenantId", true),
                    ReadText(request, "clientCode", 128, false), ReadText(request, "serviceName", 128, false),
                    ReadText(request, "audience", 128, false), ReadText(request, "inheritedPipeHandle", 128, true),
                    ReadText(request, "expectedCredentialVersion", 128, true)));
        }
        catch (JsonException)
        { throw new ServiceClientOperationalContractException("Operational input is malformed."); }
        finally
        {
            Array.Clear(buffer);
            builder.Clear();
        }
    }

    private static void ExactObject(JsonElement element, string[] properties)
    {
        if (element.ValueKind != JsonValueKind.Object) throw Malformed();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!seen.Add(property.Name) || !properties.Contains(property.Name, StringComparer.Ordinal)) throw Malformed();
        if (seen.Count != properties.Length) throw Malformed();
    }

    private static string? ReadText(JsonElement element, string name, int maximumLength, bool nullable)
    {
        var value = element.GetProperty(name);
        if (nullable && value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw Malformed();
        var text = value.GetString();
        if (string.IsNullOrEmpty(text) || text.Length > maximumLength
            || !string.Equals(text, text.Trim(), StringComparison.Ordinal) || text.Any(char.IsControl)) throw Malformed();
        return text;
    }

    private static Guid? ReadGuid(JsonElement element, string name, bool nullable)
    {
        var text = ReadText(element, name, 36, nullable);
        if (text is null) return null;
        return Guid.TryParseExact(text, "D", out var value) && value != Guid.Empty
            && string.Equals(text, value.ToString("D"), StringComparison.Ordinal) ? value : throw Malformed();
    }

    private static long ReadVersion(JsonElement element)
    {
        var value = element.GetProperty("expectedOperationalVersion");
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var version)
            && version >= 0 && version < long.MaxValue ? version : throw Malformed();
    }

    private static ServiceClientOperationalContractException Malformed() => new("Operational input is malformed.");
    private static async Task<int> FailAsync(TextWriter output, string code, int exit)
    {
        await output.WriteLineAsync(JsonSerializer.Serialize(new { status = "failed", code }));
        return exit;
    }
    private sealed record OperationalEnvelope(string AccessToken, string OperationalMarker,
        ServiceClientOperationalProvisioningRequest Request);
}
