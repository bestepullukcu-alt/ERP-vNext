using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;

public sealed record GetDocumentAccessPolicyListQuery(DocumentAccessPolicyListFilter Filter, string CorrelationId)
    : IRequest<Response<IReadOnlyList<DocumentAccessPolicyListItemModel>>>;
