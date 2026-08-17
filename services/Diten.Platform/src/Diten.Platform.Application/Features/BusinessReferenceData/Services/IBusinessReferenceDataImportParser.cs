namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed record BusinessReferenceDataImportParsedRow(
    int RowNumber,
    string? ValueCode,
    string? DisplayName,
    string? Description,
    string? ParentValueCode,
    string? ReplacementValueCode,
    bool IsDeprecated,
    int SortOrder,
    Dictionary<string, string>? Attributes);

public interface IBusinessReferenceDataImportParser
{
    string ParserKey { get; }
    bool Supports(string format, string fileName);
    // Raw file bytes — text parsers decode as UTF-8, binary parsers (xlsx) read
    // the bytes directly (e.g. as a zip archive).
    Task<IReadOnlyList<BusinessReferenceDataImportParsedRow>> ParseAsync(byte[] content, CancellationToken ct = default);
}
