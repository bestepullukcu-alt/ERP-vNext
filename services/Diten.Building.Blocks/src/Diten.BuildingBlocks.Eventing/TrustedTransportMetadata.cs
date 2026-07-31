using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json.Serialization;

namespace Diten.BuildingBlocks.Eventing;

public sealed class TrustedTransportMetadata
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public const string SignatureSchemeHeader = "X-Diten-Event-Signature-Scheme";
    public const string KeyIdHeader = "X-Diten-Event-Key-Id";
    public const string SignatureHeader = "X-Diten-Event-Signature";
    public const int MaxHeaderValueBytes = 512;
    public const int MaxTotalBytes = 1024;

    private static readonly HashSet<string> AllowedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        SignatureSchemeHeader,
        KeyIdHeader,
        SignatureHeader
    };

    private readonly IReadOnlyDictionary<string, string> _headers;

    public static TrustedTransportMetadata Empty { get; } = new([]);

    public TrustedTransportMetadata(IEnumerable<KeyValuePair<string, string>> headers)
        : this(headers, validate: true)
    {
    }

    [JsonConstructor]
    public TrustedTransportMetadata(IReadOnlyDictionary<string, string> headers)
        : this(headers.AsEnumerable(), validate: true)
    {
    }

    private TrustedTransportMetadata(
        IEnumerable<KeyValuePair<string, string>> headers,
        bool validate)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var validated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var totalBytes = 0;
        foreach (var header in headers)
        {
            ValidateName(header.Key);
            ValidateValue(header.Value);
            if (!validated.TryAdd(header.Key, header.Value))
            {
                throw new EventValidationException($"Duplicate trusted transport header '{header.Key}'.");
            }

            totalBytes += StrictUtf8.GetByteCount(header.Key) + StrictUtf8.GetByteCount(header.Value);
            if (totalBytes > MaxTotalBytes)
            {
                throw new EventValidationException("Trusted transport metadata exceeds the allowed size.");
            }
        }

        if (validated.Count is not 0 and not 3)
        {
            throw new EventValidationException(
                "Trusted signed transport metadata must contain the complete three-header set.");
        }

        _headers = new ReadOnlyDictionary<string, string>(validated);
    }

    public IReadOnlyDictionary<string, string> Headers => _headers;

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || !AllowedNames.Contains(name)
            || name.Any(char.IsWhiteSpace)
            || name.Contains('\r')
            || name.Contains('\n'))
        {
            throw new EventValidationException("Trusted transport header name is not allowed.");
        }
    }

    private static void ValidateValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(char.IsWhiteSpace)
            || value.Contains('\r')
            || value.Contains('\n')
            || StrictUtf8.GetByteCount(value) > MaxHeaderValueBytes)
        {
            throw new EventValidationException("Trusted transport header value is invalid.");
        }
    }
}
