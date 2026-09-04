namespace Diten.BuildingBlocks.Eventing;

public static class RawSignatureHeaderExtractor
{
    private static readonly HashSet<string> CanonicalNames = new(StringComparer.Ordinal)
    {
        TrustedTransportMetadata.SignatureSchemeHeader,
        TrustedTransportMetadata.KeyIdHeader,
        TrustedTransportMetadata.SignatureHeader
    };

    public static TrustedTransportMetadata Extract(IEnumerable<KeyValuePair<string, string>> rawHeaders)
    {
        ArgumentNullException.ThrowIfNull(rawHeaders);

        var accepted = new List<KeyValuePair<string, string>>(3);
        var seenIgnoringCase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in rawHeaders)
        {
            if (!seenIgnoringCase.Add(header.Key))
            {
                throw new EventContractException(
                    "Duplicate or case-duplicate signature header.",
                    "event.header.duplicate");
            }

            if (!CanonicalNames.Contains(header.Key))
            {
                throw new EventContractException(
                    "Unknown or non-canonical signature header.",
                    "event.header.unsupported");
            }

            accepted.Add(header);
        }

        try
        {
            return new TrustedTransportMetadata(accepted);
        }
        catch (EventValidationException exception)
        {
            throw new EventContractException(exception.Message, "event.header.invalid");
        }
    }
}
