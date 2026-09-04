using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws;

public sealed record DwsTrustedContext(Guid TenantId, Guid ActorId, string IdempotencyKey);
public sealed record DwsLocalResult(string Operation, string Outcome, string CorrelationId);
public sealed record DwsDispatchRequest(string Operation, IDwsRequestContract Contract, DwsTrustedContext Context)
    : IRequest<Response<DwsLocalResult>>;

public interface IDwsLocalActionExecutor
{
    Task<DwsLocalResult> ExecuteAsync(DwsDispatchRequest request, CancellationToken cancellationToken);
}

public interface IMod0117ContextValidationAdapter
{
    Task ValidateAsync(ExternalContextReference reference, CancellationToken cancellationToken);
}

public interface IFu16DwsAuthorizationAdapter
{
    Task AuthorizeAsync(Guid tenantId, Guid actorId, string operation, string permission, CancellationToken cancellationToken);
}

public interface IDwsAuditSimulator
{
    Task RecordAsync(Guid tenantId, Guid actorId, string operation, CancellationToken cancellationToken);
}

public sealed record DwsSelfRegistrationContract(
    string ModuleCode,
    string DisplayName,
    string RoutePath,
    string Shell,
    IReadOnlyList<string> Permissions);

public static class DwsSelfRegistration
{
    public static DwsSelfRegistrationContract Contract { get; } = new(
        "MOD-0354",
        "Decomposition & Work Structuring Engine",
        "/management-governance/delivery-execution/structures",
        "tenant",
        DwsAuthorizationManifest.Entries.Select(entry => entry.Permission).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
}
