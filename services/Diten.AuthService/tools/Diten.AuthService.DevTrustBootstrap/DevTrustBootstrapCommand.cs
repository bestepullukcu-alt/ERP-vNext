using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.ServiceClientCredentialGenerator;

namespace Diten.AuthService.DevTrustBootstrap;

/// <summary>
/// MOD-0033-FU02 §F.3 — Development bootstrap of the service-to-service trust chain (MDM → Auth → Platform).
///
/// <para>Generates a disposable RSA-2048 key pair and a service-client credential pair and writes ONE
/// <c>appsettings.Development.local.json</c> into each of the three target directories:
/// Auth (token issuer key + client registry, HASH only), Platform (public key), MDM (the raw client secret,
/// workflow switch and approver candidates). Every file is created new and owner-only.</para>
///
/// <para>It never overwrites: if all three files are already there it reports "already set up" and changes nothing;
/// if only some are there it stops and changes nothing, because the three files only work as one generated set.
/// Nothing secret is printed, and no secret is taken from or passed on a command line.</para>
/// </summary>
public static class DevTrustBootstrapCommand
{
    public const string LocalSettingsFileName = "appsettings.Development.local.json";
    public const string DefaultClientCode = "diten-mdm-workflow";
    public const string DefaultIssuer = "diten-auth-service-identity";
    public const string DefaultAuthBaseUrl = "http://localhost:5056";
    public const string ServiceName = "Diten.MDM";
    public const string Audience = "TRUSTED_WORKFLOW_CONSUMER";
    public const string GlobalProductObjectType = "GlobalProduct";
    /// <summary>The tracked Development value of MDM's <c>ProductIdentityWorkflow:GlobalProductTemplateCode</c>.</summary>
    public const string DefaultTemplateCode = "GLOBAL-PRODUCT-IDENTITY";

    /// <summary>
    /// WP-MDM-GSKU-ACCEPT-01 FIX1 (B6): the GSKU starts, by the object type each processor sends and the tracked
    /// Development template code of each (MDM appsettings.Development.json). Platform matches all six fields exactly.
    /// </summary>
    public static readonly (string ObjectType, string TemplateCode)[] GskuStarts =
    [
        ("gsku", "GSKU-IDENTITY"),
        ("GskuCorrection", "GSKU-CORRECTION"),
        ("GskuRetirementRequest", "GSKU-RETIREMENT-REQUEST")
    ];

    public const int Success = 0;
    public const int UsageError = 2;
    public const int StateConflict = 3;
    public const int UnexpectedError = 4;

    public const string Usage =
        "usage: --auth-dir <dir> --platform-dir <dir> --mdm-dir <dir> --tenant-id <guid> [--tenant-id <guid> ...] "
        + "--approver-user-id <guid> [--approver-user-id <guid> ...] [--client-code <code>] [--issuer <text>] "
        + "[--auth-base-url <url>]";

    public const string AuthBaseUrlRefusal =
        "refused: --auth-base-url must be an http or https URL of THIS machine (host exactly localhost, 127.0.0.1 "
        + "or [::1]) with no user info, path, query or fragment; nothing was changed.";

    private static readonly string[] AuthSections = ["ServiceIdentityTokenIssuer", "ServiceClientRegistry"];
    private static readonly string[] PlatformSections = ["TrustedServiceTokenValidation"];
    private static readonly string[] MdmSections =
    [
        "AuthProductIdentityWorkflowServiceIdentityProvider", "ProductIdentityWorkflow", "ProductIdentityWorkflowWorker",
        // GSKU FIX1 (B6): the GSKU identity, correction and retirement-request switches and their workers.
        "FirstGskuIdentityWorkflow", "FirstGskuIdentityWorkflowWorker",
        "GskuCorrectionWorkflow", "GskuCorrectionWorkflowWorker",
        "GskuRetirementRequestWorkflow", "GskuRetirementRequestWorkflowWorker"
    ];

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(args, out var options, out var refusal))
        {
            // A refusal names the argument and never repeats its value (a URL can carry user info).
            error.WriteLine(refusal ?? Usage);
            return UsageError;
        }

        try
        {
            var targets = new[]
            {
                new Target("Auth", Path.Combine(Path.GetFullPath(options.AuthDirectory), LocalSettingsFileName), AuthSections),
                new Target("Platform", Path.Combine(Path.GetFullPath(options.PlatformDirectory), LocalSettingsFileName), PlatformSections),
                new Target("MDM", Path.Combine(Path.GetFullPath(options.MdmDirectory), LocalSettingsFileName), MdmSections)
            };
            // Case-insensitively: on a case-insensitive file system two spellings are one directory.
            if (targets.Select(target => target.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Length)
            {
                error.WriteLine("refused: the three target directories must be different.");
                return UsageError;
            }

            foreach (var target in targets)
            {
                if (!Directory.Exists(Path.GetDirectoryName(target.Path)!))
                {
                    error.WriteLine($"refused: the {target.Service} target directory does not exist; nothing was changed.");
                    return UsageError;
                }
            }

            var existing = targets.Where(target => File.Exists(target.Path)).ToArray();
            if (existing.Length == targets.Length)
            {
                var incomplete = existing.Where(target => !HasSections(target)).ToArray();
                if (incomplete.Length == 0)
                {
                    output.WriteLine("already set up: all three local settings files exist; nothing was changed.");
                    return Success;
                }

                error.WriteLine("refused: these local settings files exist but are not a complete setup; nothing was changed:");
                foreach (var target in incomplete) error.WriteLine("  " + target.Path);
                return StateConflict;
            }

            if (existing.Length != 0)
            {
                error.WriteLine("refused: a partial setup exists; the three files only work as one generated set. Nothing was changed.");
                foreach (var target in targets)
                    error.WriteLine($"  {(File.Exists(target.Path) ? "exists " : "missing")} {target.Path}");
                error.WriteLine("Move the existing file(s) aside yourself, then run this again.");
                return StateConflict;
            }

            Write(options, targets);

            foreach (var target in targets) output.WriteLine("written " + target.Path);
            output.WriteLine("ClientCode=" + options.ClientCode);
            output.WriteLine("ClientId=" + ServiceClientIdentityIds.FromClientCode(options.ClientCode).ToString("D"));
            output.WriteLine("Restart Auth, Platform and MDM so they load the new local settings.");
            return Success;
        }
        catch (Exception exception)
        {
            // Type name only — never a message that could quote generated material.
            error.WriteLine("failed: " + exception.GetType().Name);
            return UnexpectedError;
        }
    }

    private static void Write(Options options, Target[] targets)
    {
        var keyId = "dev-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var credentialVersion = "dev-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");

        using var rsa = RSA.Create(2048);
        var platformJson = Json(writer =>
        {
            writer.WriteStartObject("TrustedServiceTokenValidation");
            writer.WriteString("Issuer", options.Issuer);
            writer.WriteString("CurrentKeyId", keyId);
            writer.WriteString("CurrentPublicKeyPem", rsa.ExportSubjectPublicKeyInfoPem());
            writer.WriteEndObject();
            if (!string.Equals(options.ClientCode, DefaultClientCode, StringComparison.Ordinal))
            {
                // Platform's tracked Development entry authorizes the DEFAULT client's id. A different client code
                // (the way out when the first credential set is lost) has a different id, so its entry is written
                // here; as entry 0 it takes the place of the tracked one. Not a secret.
                writer.WriteStartObject("TrustedWorkflowStartAuthorization");
                writer.WriteStartArray("Entries");
                writer.WriteStartObject();
                writer.WriteString("ClientId", ServiceClientIdentityIds.FromClientCode(options.ClientCode).ToString("D"));
                writer.WriteString("ServiceName", ServiceName);
                writer.WriteString("Audience", Audience);
                writer.WriteString("ObjectType", GlobalProductObjectType);
                writer.WriteString("TemplateCode", DefaultTemplateCode);
                writer.WriteEndObject();
                // Entries 1–3, in the tracked file's order: they replace the tracked GSKU entries, which carry the
                // default client's id.
                foreach (var (objectType, templateCode) in GskuStarts)
                {
                    writer.WriteStartObject();
                    writer.WriteString("ClientId", ServiceClientIdentityIds.FromClientCode(options.ClientCode).ToString("D"));
                    writer.WriteString("ServiceName", ServiceName);
                    writer.WriteString("Audience", Audience);
                    writer.WriteString("ObjectType", objectType);
                    writer.WriteString("TemplateCode", templateCode);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
        });

        string mdmJson = string.Empty;
        var credentialHash = ServiceClientCredentialFactory.Create(rawSecret =>
        {
            var secret = new string(rawSecret);
            mdmJson = Json(writer =>
            {
                writer.WriteStartObject("AuthProductIdentityWorkflowServiceIdentityProvider");
                writer.WriteString("AuthBaseUrl", options.AuthBaseUrl);
                writer.WriteString("ExpectedIssuer", options.Issuer);
                writer.WriteString("ClientId", options.ClientCode);
                writer.WriteString("ActiveClientSecret", secret);
                writer.WriteEndObject();
                writer.WriteStartObject("ProductIdentityWorkflow");
                writer.WriteBoolean("Enabled", true);
                writer.WriteStartArray("CandidatePrincipalIds");
                foreach (var approver in options.ApproverUserIds) writer.WriteStringValue(approver.ToString("D"));
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteStartObject("ProductIdentityWorkflowWorker");
                writer.WriteBoolean("Enabled", true);
                writer.WriteEndObject();
                // GSKU FIX1 (B6): the same switches for the three GSKU flows (template and reason codes are tracked in
                // MDM's appsettings.Development.json) and their workers — without a worker a decision taken in the
                // Task Center never reaches MDM.
                foreach (var section in new[] { "FirstGskuIdentityWorkflow", "GskuCorrectionWorkflow", "GskuRetirementRequestWorkflow" })
                {
                    writer.WriteStartObject(section);
                    writer.WriteBoolean("Enabled", true);
                    writer.WriteStartArray("CandidatePrincipalIds");
                    foreach (var approver in options.ApproverUserIds) writer.WriteStringValue(approver.ToString("D"));
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                foreach (var section in new[] { "FirstGskuIdentityWorkflowWorker", "GskuCorrectionWorkflowWorker", "GskuRetirementRequestWorkflowWorker" })
                {
                    writer.WriteStartObject(section);
                    writer.WriteBoolean("Enabled", true);
                    writer.WriteEndObject();
                }
            });
        });

        var authJson = Json(writer =>
        {
            writer.WriteStartObject("ServiceIdentityTokenIssuer");
            writer.WriteString("Issuer", options.Issuer);
            writer.WriteString("ActiveKeyId", keyId);
            writer.WriteString("ActivePrivateKeyPem", rsa.ExportPkcs8PrivateKeyPem());
            writer.WriteNumber("TokenLifetimeSeconds", 300);
            writer.WriteEndObject();
            writer.WriteStartObject("ServiceClientRegistry");
            writer.WriteStartArray("Clients");
            writer.WriteStartObject();
            writer.WriteString("ClientCode", options.ClientCode);
            writer.WriteString("ServiceName", ServiceName);
            writer.WriteString("AllowedAudience", Audience);
            writer.WriteString("CredentialHash", credentialHash);
            writer.WriteString("CredentialVersion", credentialVersion);
            writer.WriteStartArray("Tenants");
            foreach (var tenant in options.TenantIds) writer.WriteStringValue(tenant.ToString("D"));
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        // Staged under a temporary name in the same directory, then moved without overwrite: a failure part-way
        // leaves no half-written final file, and a file that appeared meanwhile is never replaced.
        var contents = new[] { authJson, platformJson, mdmJson };
        var staged = new List<(string Temporary, string Final)>();
        try
        {
            for (var index = 0; index < targets.Length; index++)
            {
                var temporary = targets[index].Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                OwnerOnlyFile.CreateNew(temporary, contents[index]);
                staged.Add((temporary, targets[index].Path));
            }

            var moved = new List<string>();
            try
            {
                foreach (var (temporary, final) in staged)
                {
                    File.Move(temporary, final, overwrite: false);
                    moved.Add(final);
                }
            }
            catch
            {
                // All three or none: a file this run put in place is taken back, so no partial set is left.
                foreach (var final in moved) File.Delete(final);
                throw;
            }
        }
        finally
        {
            foreach (var (temporary, _) in staged)
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    private static string Json(Action<Utf8JsonWriter> body)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length) + "\n";
    }

    private static bool HasSections(Target target)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(target.Path));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && target.Sections.All(section => document.RootElement.TryGetProperty(section, out _));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParse(string[] args, out Options options, out string? refusal)
    {
        options = default!;
        refusal = null;
        string? authDirectory = null, platformDirectory = null, mdmDirectory = null;
        string? clientCode = null, issuer = null, authBaseUrl = null;
        var tenants = new List<Guid>();
        var approvers = new List<Guid>();
        if (args.Length % 2 != 0) return false;
        for (var index = 0; index < args.Length; index += 2)
        {
            var value = args[index + 1];
            switch (args[index])
            {
                case "--auth-dir" when authDirectory is null: authDirectory = value; break;
                case "--platform-dir" when platformDirectory is null: platformDirectory = value; break;
                case "--mdm-dir" when mdmDirectory is null: mdmDirectory = value; break;
                case "--client-code" when clientCode is null: clientCode = value; break;
                case "--issuer" when issuer is null: issuer = value; break;
                case "--auth-base-url" when authBaseUrl is null: authBaseUrl = value; break;
                case "--tenant-id":
                    if (!TryGuid(value, out var tenant) || tenants.Contains(tenant)) return false;
                    tenants.Add(tenant);
                    break;
                case "--approver-user-id":
                    if (!TryGuid(value, out var approver) || approvers.Contains(approver)) return false;
                    approvers.Add(approver);
                    break;
                default: return false;
            }
        }

        clientCode ??= DefaultClientCode;
        issuer ??= DefaultIssuer;
        authBaseUrl ??= DefaultAuthBaseUrl;
        if (string.IsNullOrWhiteSpace(authDirectory) || string.IsNullOrWhiteSpace(platformDirectory)
            || string.IsNullOrWhiteSpace(mdmDirectory) || tenants.Count == 0
            || approvers.Count is 0 or > 100
            || !IsExact(clientCode, 128) || !IsExact(issuer, 256))
        {
            return false;
        }

        if (!IsLocalAuthBaseUrl(authBaseUrl))
        {
            refusal = AuthBaseUrlRefusal;
            return false;
        }

        options = new Options(authDirectory, platformDirectory, mdmDirectory, clientCode, issuer, authBaseUrl,
            tenants, approvers);
        return true;
    }

    /// <summary>
    /// The value becomes MDM's <c>AuthProductIdentityWorkflowServiceIdentityProvider:AuthBaseUrl</c>, and MDM sends
    /// the RAW client secret there. A Development tool must not be able to point that secret at another machine, so
    /// the host has to be exactly <c>localhost</c>, <c>127.0.0.1</c> or <c>[::1]</c> (as <see cref="Uri"/> reports
    /// it — a name that merely starts with "localhost" is another host). Everything else is what MDM's own validator
    /// (<c>AuthProductIdentityWorkflowServiceIdentityProvider.EnsureValidConfiguration</c>) requires — absolute
    /// http/https, path "/", no user info, no query, no fragment — so the bootstrap never writes a value MDM rejects.
    /// </summary>
    public static bool IsLocalAuthBaseUrl(string? value) =>
        value is not null && IsExact(value, 256)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && uri.Host is "localhost" or "127.0.0.1" or "[::1]";

    private static bool TryGuid(string value, out Guid id) =>
        Guid.TryParseExact(value, "D", out id) && id != Guid.Empty;

    private static bool IsExact(string value, int maximum) =>
        value.Length is > 0 && value.Length <= maximum
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl) && !value.Contains(',');

    private sealed record Target(string Service, string Path, string[] Sections);

    private sealed record Options(
        string AuthDirectory,
        string PlatformDirectory,
        string MdmDirectory,
        string ClientCode,
        string Issuer,
        string AuthBaseUrl,
        IReadOnlyList<Guid> TenantIds,
        IReadOnlyList<Guid> ApproverUserIds);
}
