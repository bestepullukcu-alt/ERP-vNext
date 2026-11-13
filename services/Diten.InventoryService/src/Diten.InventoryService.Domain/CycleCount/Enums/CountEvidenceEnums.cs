namespace Diten.InventoryService.Domain.CycleCounting.Enums;

/// <summary>DL-5 (pack §4.5, Q-18 = A): the two Evidence Pack formats — same content.</summary>
public enum CountEvidenceFormat
{
    Pdf,
    Csv
}

/// <summary>
/// DL-5 (pack §4.5): <c>Ready</c> = the file is stored and hashed. <c>Failed</c> is named by the pack and reserved: v1 writes an export only after
/// its file was drawn, so it never stores a Failed one (a renderer failure answers the error and stores nothing).
/// </summary>
public enum CountEvidenceStatus
{
    Ready,
    Failed
}
