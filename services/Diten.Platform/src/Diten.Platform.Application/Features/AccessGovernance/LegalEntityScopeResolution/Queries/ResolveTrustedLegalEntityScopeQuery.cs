using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Queries;

public sealed record ResolveTrustedLegalEntityScopeQuery(Guid TenantId, Guid SubjectId, string ModuleCode, string PermissionKey)
    : IRequest<Response<TrustedLegalEntityScopeResolution>>;
