using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record DryRunQmsBaselineImportCommand(
    string FileName,
    string Format,
    string ContentBase64,
    string SourceBaselineKey,
    string CorrelationId) : IRequest<Response<QmsBaselineImportSummary>>;
