using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record CommitQmsBaselineImportCommand(
    string FileName,
    string Format,
    string ContentBase64,
    string SourceBaselineKey,
    string BaselineVersion,
    string? ChangeSummary,
    string CorrelationId) : IRequest<Response<QmsBaselineCommitResult>>;
