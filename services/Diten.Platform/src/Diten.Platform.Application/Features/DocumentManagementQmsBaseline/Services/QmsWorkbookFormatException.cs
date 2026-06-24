namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Raised when the workbook does not satisfy the FU02 source-format contract (e.g. the canonical sheet is missing).
/// The import service maps it to a controlled <c>VALIDATION_FAILED</c> summary — never a fabricated success.
/// </summary>
public sealed class QmsWorkbookFormatException : Exception
{
    public QmsWorkbookFormatException(string reasonMessage)
        : base(reasonMessage)
    {
    }
}
