using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.CrmService.Application.Features.VisitQr;

namespace Diten.CrmService.Api.Security;

/// <summary>
/// WP-VW-W3E (W3E-BE-a, §2.2) — the visit QR signing keys from the secret store (<see cref="ISecretsProvider"/>): every
/// <c>crm-visit-qr-signing:&lt;keyId&gt;</c> secret (base64, ≥ 32 bytes; in Production the environment variable
/// <c>crm-visit-qr-signing__&lt;keyId&gt;</c>) is a key, <c>Crm:VisitQr:ActiveKeyId</c> names the one new codes are signed
/// with; the others keep verifying the codes they signed (rotation). No key value is ever in code, appsettings or a log.
/// <para><b>Development only:</b> with no usable active key a deterministic development key (<c>dev</c>) is used and a
/// warning is logged. Anywhere else no key = unavailable: issue / print answer 503 <c>visit_qr_signing_unavailable</c>,
/// a required QR start 503, an optional one starts with <c>qr_unverifiable</c>.</para>
/// </summary>
public sealed class SecretsVisitQrKeyRing : IVisitQrKeyRing
{
    public const string SecretPrefix = "crm-visit-qr-signing";
    public const string ActiveKeyIdSetting = "Crm:VisitQr:ActiveKeyId";
    public const string DevelopmentKeyId = "dev";

    private readonly ISecretsProvider _secrets;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SecretsVisitQrKeyRing> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private VisitQrKeys? _cached;

    public SecretsVisitQrKeyRing(
        ISecretsProvider secrets, IConfiguration configuration, IHostEnvironment environment, ILogger<SecretsVisitQrKeyRing> logger)
    {
        _secrets = secrets;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<VisitQrKeys> GetAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _cached ??= await LoadAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<VisitQrKeys> LoadAsync(CancellationToken cancellationToken)
    {
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            foreach (var (name, value) in await _secrets.GetSecretsAsync(SecretPrefix, cancellationToken))
            {
                var keyId = name[(name.LastIndexOf(':') + 1)..];
                if (VisitQrSigner.IsValidKeyId(keyId) && TryDecode(value) is { Length: >= VisitQrSigner.MinKeyBytes } bytes)
                {
                    keys[keyId] = bytes;
                }
                else
                {
                    _logger.LogWarning("A visit QR signing secret was ignored: its key id or value is not usable (base64, at least {Bytes} bytes).", VisitQrSigner.MinKeyBytes);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The visit QR signing keys could not be read.");
        }

        var active = _configuration[ActiveKeyIdSetting]?.Trim();
        var result = new VisitQrKeys(string.IsNullOrEmpty(active) ? null : active, keys);
        if (result.Available)
        {
            return result;
        }

        if (_environment.IsDevelopment())
        {
            keys[DevelopmentKeyId] = DevelopmentKey();
            _logger.LogWarning(
                "No visit QR signing key is configured; the DEVELOPMENT key '{KeyId}' is used. Configure {Prefix}:<keyId> and {Setting} before any other environment.",
                DevelopmentKeyId, SecretPrefix, ActiveKeyIdSetting);
            return new VisitQrKeys(DevelopmentKeyId, keys);
        }

        _logger.LogError("No visit QR signing key is configured ({Setting}); QR codes cannot be issued or verified.", ActiveKeyIdSetting);
        return result;
    }

    /// <summary>The deterministic development key (never valid outside Development: the ring only adds it there).</summary>
    public static byte[] DevelopmentKey() => SHA256.HashData(Encoding.UTF8.GetBytes("diten-crm-visit-qr-development-key-not-for-production"));

    private static byte[]? TryDecode(string value)
    {
        try
        {
            return Convert.FromBase64String(value.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
