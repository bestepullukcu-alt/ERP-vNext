using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;

public sealed record GetDocumentAccessTargetOptionsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<DocumentAccessPolicyTargetModel>>>;
