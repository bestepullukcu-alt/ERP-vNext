namespace Diten.Platform.Infrastructure.Persistence.Repositories.ESignature;

internal sealed class SignatureEnvelopeCounter
{
    public string Id { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public int Year { get; set; }
    public long LastSequence { get; set; }
}
