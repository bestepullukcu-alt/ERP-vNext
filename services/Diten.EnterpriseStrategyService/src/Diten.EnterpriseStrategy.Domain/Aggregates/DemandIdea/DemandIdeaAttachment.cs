namespace Diten.Domain.Aggregates.DemandIdea;

public sealed class DemandIdeaAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    /// <summary>Relative path under upload root, e.g. demand-ideas/{demandId}/{storedName}</summary>
    public string StorageKey { get; set; } = string.Empty;
}
