using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure.Audit;

namespace Diten.MdmService.Api.Configuration;

/// <summary>One immutable snapshot of process-only configuration. Never bind this through application configuration.</summary>
public sealed class SelectedAuditIntentDeliveryOptions
{
    public const string EnabledVariable = "SelectedAuditIntentDelivery__Enabled";
    public const string ManifestVariable = "SelectedAuditIntentDelivery__Manifest";
    public const int MaximumManifestCharacters = 65536;
    private readonly string _mongoConnectionString;
    private readonly AuthTrustedSourceAuditServiceIdentityProviderOptions _identityOptions;
    private readonly TrustedSourceAuditIntentClientOptions _clientOptions;

    private SelectedAuditIntentDeliveryOptions(SelectedAuditIntentDeliveryRequest selection, string operatorSid,
        string operatorAccount, string markerSha256, string mongoConnectionString, string databaseName,
        AuthTrustedSourceAuditServiceIdentityProviderOptions identityOptions,
        TrustedSourceAuditIntentClientOptions clientOptions, int leaseSeconds, int retrySeconds,
        int maximumAttempts, int overallSeconds, int markerSeconds)
    {
        Selection = selection;
        OperatorSid = operatorSid;
        OperatorAccount = operatorAccount;
        MarkerSha256 = markerSha256;
        _mongoConnectionString = mongoConnectionString;
        DatabaseName = databaseName;
        _identityOptions = identityOptions;
        _clientOptions = clientOptions;
        LeaseDuration = TimeSpan.FromSeconds(leaseSeconds);
        RetryDelay = TimeSpan.FromSeconds(retrySeconds);
        MaximumAttempts = maximumAttempts;
        OverallBudget = TimeSpan.FromSeconds(overallSeconds);
        MarkerBudget = TimeSpan.FromSeconds(markerSeconds);
    }

    public SelectedAuditIntentDeliveryRequest Selection { get; }
    public string OperatorSid { get; }
    public string OperatorAccount { get; }
    public string MarkerSha256 { get; }
    public string DatabaseName { get; }
    public TimeSpan LeaseDuration { get; }
    public TimeSpan RetryDelay { get; }
    public int MaximumAttempts { get; }
    public TimeSpan OverallBudget { get; }
    public TimeSpan MarkerBudget { get; }
    internal string MongoConnectionString => _mongoConnectionString;
    internal AuthTrustedSourceAuditServiceIdentityProviderOptions IdentityOptions => _identityOptions;
    internal TrustedSourceAuditIntentClientOptions ClientOptions => _clientOptions;

    public static SelectedAuditIntentDeliveryOptions CaptureProcess()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process))
            if (entry.Key is string key) values.Add(key, entry.Value as string);
        return Capture(new ReadOnlyDictionary<string, string?>(values));
    }

    /// <summary>Deterministic snapshot seam. Production calls CaptureProcess, never an injected configuration provider.</summary>
    public static SelectedAuditIntentDeliveryOptions Capture(IReadOnlyDictionary<string, string?> processValues)
    {
        ArgumentNullException.ThrowIfNull(processValues);
        var values = processValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        try
        {
            if (Read(values, "ASPNETCORE_ENVIRONMENT") != "Development"
                || Read(values, "DOTNET_ENVIRONMENT") != "Development")
                throw Invalid("ENVIRONMENT_DENIED");
            if (Read(values, EnabledVariable) != "true") throw Invalid("DISABLED");
            RejectUnknownExecutionVariables(values);
            var manifest = Read(values, ManifestVariable);
            if (manifest.Length is < 2 or > MaximumManifestCharacters) throw Invalid("MANIFEST_INVALID");
            using var json = JsonDocument.Parse(manifest, new JsonDocumentOptions
            {
                AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 8
            });
            var root = json.RootElement;
            RequireProperties(root,
                ["schemaVersion", "executionId", "tenantId", "operatorSid", "operatorAccount", "markerSha256",
                 "mongoConnectionString", "databaseName", "authBaseUrl", "platformBaseUrl", "items"],
                ["leaseSeconds", "retrySeconds", "maximumAttempts", "overallSeconds", "markerSeconds"]);
            if (root.GetProperty("schemaVersion").GetInt32() != 1) throw Invalid("SCHEMA_INVALID");
            var tenantId = ReadGuid(root, "tenantId");
            var operatorSid = ReadString(root, "operatorSid", 184);
            var operatorAccount = ReadString(root, "operatorAccount", 256);
            if (!Regex.IsMatch(operatorSid, @"^S-1-(?:[0-9]{1,10}-){1,14}[0-9]{1,10}$", RegexOptions.CultureInvariant)
                || !Regex.IsMatch(operatorAccount, @"^[^\\\s]+\\[^\\\s]+$", RegexOptions.CultureInvariant))
                throw Invalid("OPERATOR_INVALID");
            var markerSha256 = ReadString(root, "markerSha256", 64);
            if (markerSha256.Length != 64 || markerSha256.Any(character => !Uri.IsHexDigit(character)))
                throw Invalid("MARKER_DIGEST_INVALID");
            var connectionString = ReadString(root, "mongoConnectionString", 2048);
            ValidateMongoEndpoint(connectionString);
            var databaseName = ReadString(root, "databaseName", 63);
            if (!Regex.IsMatch(databaseName, @"^[A-Za-z0-9][A-Za-z0-9_-]{0,62}$", RegexOptions.CultureInvariant)
                || databaseName is "admin" or "config" or "local") throw Invalid("DATABASE_INVALID");
            var authBaseUrl = ReadString(root, "authBaseUrl", 256);
            var platformBaseUrl = ReadString(root, "platformBaseUrl", 256);
            ValidateLocalHttpEndpoint(authBaseUrl);
            ValidateLocalHttpEndpoint(platformBaseUrl);
            if (authBaseUrl == platformBaseUrl) throw Invalid("ENDPOINTS_AMBIGUOUS");

            var itemElements = root.GetProperty("items");
            if (itemElements.ValueKind != JsonValueKind.Array || itemElements.GetArrayLength() is < 1 or > 100)
                throw Invalid("SELECTION_INVALID");
            var items = new List<SelectedAuditIntentDeliveryItem>();
            foreach (var item in itemElements.EnumerateArray())
            {
                RequireProperties(item, ["tenantId", "aggregateType", "aggregateId", "intentId", "expectedClaimGeneration", "evidenceFingerprint"], []);
                var aggregateTypeName = ReadString(item, "aggregateType", 64);
                if (!Enum.TryParse<AuditAggregateType>(aggregateTypeName, false, out var aggregateType)
                    || aggregateType.ToString() != aggregateTypeName || !SelectedAuditIntentDeliveryRequest.IsAllowed(aggregateType))
                    throw Invalid("AGGREGATE_DENIED");
                items.Add(new SelectedAuditIntentDeliveryItem(
                    new AuditIntentLocator(ReadGuid(item, "tenantId"), aggregateType,
                        ReadGuid(item, "aggregateId"), ReadGuid(item, "intentId")),
                    item.GetProperty("expectedClaimGeneration").GetInt64(), ReadString(item, "evidenceFingerprint", 64)));
            }
            var selection = new SelectedAuditIntentDeliveryRequest(ReadGuid(root, "executionId"), tenantId, items);
            var identityPrefix = AuthTrustedSourceAuditServiceIdentityProviderOptions.SectionName + "__";
            var platformPrefix = TrustedSourceAuditIntentClientOptions.SectionName + "__";
            var previousSecret = Optional(values, identityPrefix + "PreviousClientSecret");
            var previousUntilText = Optional(values, identityPrefix + "PreviousClientSecretValidUntilUtc");
            DateTimeOffset? previousUntil = null;
            if (previousUntilText is not null)
            {
                if (!DateTimeOffset.TryParseExact(previousUntilText, "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsed) || parsed.Offset != TimeSpan.Zero)
                    throw Invalid("CREDENTIAL_OVERLAP_INVALID");
                previousUntil = parsed;
            }
            var identityOptions = new AuthTrustedSourceAuditServiceIdentityProviderOptions
            {
                AuthBaseUrl = Read(values, identityPrefix + "AuthBaseUrl"),
                ExpectedIssuer = Read(values, identityPrefix + "ExpectedIssuer"),
                ClientId = Read(values, identityPrefix + "ClientId"),
                ActiveClientSecret = Read(values, identityPrefix + "ActiveClientSecret"),
                PreviousClientSecret = previousSecret,
                PreviousClientSecretValidUntilUtc = previousUntil,
                RefreshSkewSeconds = OptionalInteger(values, identityPrefix + "RefreshSkewSeconds", 30, 1, 120)
            };
            var clientOptions = new TrustedSourceAuditIntentClientOptions
            {
                PlatformBaseUrl = Read(values, platformPrefix + "PlatformBaseUrl")
            };
            if (identityOptions.AuthBaseUrl != authBaseUrl || clientOptions.PlatformBaseUrl != platformBaseUrl
                || previousSecret is not null && previousSecret == identityOptions.ActiveClientSecret)
                throw Invalid("MANIFEST_BINDING_INVALID");
            AuthTrustedSourceAuditServiceIdentityProvider.EnsureValidConfiguration(identityOptions);
            PlatformTrustedSourceAuditIntentClient.EnsureValidConfiguration(clientOptions);
            return new SelectedAuditIntentDeliveryOptions(selection, operatorSid, operatorAccount, markerSha256,
                connectionString, databaseName, identityOptions, clientOptions,
                OptionalInteger(root, "leaseSeconds", 60, 10, 900), OptionalInteger(root, "retrySeconds", 30, 1, 3600),
                OptionalInteger(root, "maximumAttempts", 5, 1, 20), OptionalInteger(root, "overallSeconds", 120, 10, 900),
                OptionalInteger(root, "markerSeconds", 10, 1, 30));
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("SELECTED_AUDIT_INTENT_", StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException
            or FormatException or OverflowException or KeyNotFoundException)
        {
            // Do not include parser/configuration messages or inner exceptions, which may contain secrets.
            throw Invalid("CONFIGURATION_INVALID");
        }
    }

    private static void RejectUnknownExecutionVariables(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var key in values.Keys)
        {
            if (key.StartsWith("SelectedAuditIntentDelivery", StringComparison.OrdinalIgnoreCase)
                && key != EnabledVariable && key != ManifestVariable) throw Invalid("CONFIGURATION_KEY_INVALID");
            foreach (var exact in new[] { "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", EnabledVariable, ManifestVariable })
                if (string.Equals(key, exact, StringComparison.OrdinalIgnoreCase) && key != exact)
                    throw Invalid("CONFIGURATION_KEY_INVALID");
        }
    }

    private static void RequireProperties(JsonElement value, string[] required, string[] optional)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid("MANIFEST_INVALID");
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!found.Add(property.Name) || !required.Contains(property.Name, StringComparer.Ordinal)
                && !optional.Contains(property.Name, StringComparer.Ordinal)) throw Invalid("MANIFEST_PROPERTY_INVALID");
        if (required.Any(name => !found.Contains(name))) throw Invalid("MANIFEST_PROPERTY_MISSING");
    }

    private static string ReadString(JsonElement value, string name, int maximumLength)
    {
        var property = value.GetProperty(name);
        if (property.ValueKind != JsonValueKind.String) throw Invalid("MANIFEST_VALUE_INVALID");
        var text = property.GetString()!;
        if (text.Length == 0 || text.Length > maximumLength || text.Trim() != text || text.Any(char.IsControl))
            throw Invalid("MANIFEST_VALUE_INVALID");
        return text;
    }

    private static Guid ReadGuid(JsonElement value, string name)
    {
        if (!Guid.TryParseExact(ReadString(value, name, 36), "D", out var id) || id == Guid.Empty)
            throw Invalid("IDENTITY_INVALID");
        return id;
    }

    private static string Read(IReadOnlyDictionary<string, string?> values, string name)
        => Optional(values, name) ?? throw Invalid("CONFIGURATION_MISSING");

    private static string? Optional(IReadOnlyDictionary<string, string?> values, string name)
    {
        var variants = values.Keys.Where(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (variants.Length > 1 || variants.Length == 1 && variants[0] != name) throw Invalid("CONFIGURATION_KEY_INVALID");
        if (!values.TryGetValue(name, out var value)) return null;
        if (string.IsNullOrWhiteSpace(value)) throw Invalid("CONFIGURATION_EMPTY");
        return value;
    }

    private static int OptionalInteger(JsonElement value, string name, int fallback, int minimum, int maximum)
    {
        var result = value.TryGetProperty(name, out var element) ? element.GetInt32() : fallback;
        if (result < minimum || result > maximum) throw Invalid("BUDGET_INVALID");
        return result;
    }

    private static int OptionalInteger(IReadOnlyDictionary<string, string?> values, string name, int fallback, int minimum, int maximum)
    {
        var text = Optional(values, name);
        if (text is null) return fallback;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            throw Invalid("BUDGET_INVALID");
        return value;
    }

    private static void ValidateLocalHttpEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.Host is not ("127.0.0.1" or "localhost" or "[::1]") || !uri.IsLoopback
            || uri.Port is < 1 or > 65535 || uri.AbsolutePath != "/" || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.OriginalString != uri.AbsoluteUri)
            throw Invalid("ENDPOINT_INVALID");
    }

    private static void ValidateMongoEndpoint(string value)
    {
        // A single literal loopback seed and direct connection prohibit replica discovery from escaping the approved endpoint.
        var match = Regex.Match(value,
            @"^mongodb://(?:127\.0\.0\.1|localhost|\[::1\]):(?<port>[0-9]{1,5})/\?replicaSet=[A-Za-z0-9_-]{1,64}&directConnection=true$",
            RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups["port"].Value, out var port) || port is < 1 or > 65535)
            throw Invalid("MONGO_ENDPOINT_INVALID");
    }

    private static InvalidOperationException Invalid(string suffix) => new("SELECTED_AUDIT_INTENT_" + suffix);
}
