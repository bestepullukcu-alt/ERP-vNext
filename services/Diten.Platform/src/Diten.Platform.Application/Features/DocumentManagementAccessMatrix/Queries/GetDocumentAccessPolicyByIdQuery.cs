using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;

public sealed record GetDocumentAccessPolicyByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<DocumentAccessPolicyDetailModel>>;
