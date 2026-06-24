namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Parses an approved QMS folder workbook into raw import rows. The implementation consumes a byte buffer / stream
/// abstraction so it is identical under an API request and under a test fixture. Governance metadata only — never a
/// physical folder, document upload, or content store.
/// </summary>
public interface IQmsFolderImportParser
{
    bool Supports(string format, string fileName);

    Task<IReadOnlyList<QmsFolderImportRow>> ParseAsync(byte[] content, CancellationToken ct = default);
}
