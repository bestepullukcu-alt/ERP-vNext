using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;

public sealed record GetEffectiveDocumentAccessQuery(
    string TargetType,
    string TargetId,
    string PrincipalType,
    string PrincipalId,
    string CorrelationId)
    : IRequest<Response<EffectiveDocumentAccessModel>>;
