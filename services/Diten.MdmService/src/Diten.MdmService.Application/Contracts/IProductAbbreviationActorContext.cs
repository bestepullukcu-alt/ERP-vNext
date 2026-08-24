namespace Diten.MdmService.Application.Contracts;

public interface IProductAbbreviationActorContext
{
    Guid TenantId { get; }
    bool TenantIsResolved { get; }
    bool IsAuthenticated { get; }
    string ActorType { get; }
    string CanonicalHumanSubjectId { get; }
    IReadOnlySet<string> GrantedPermissions { get; }
    string CorrelationId { get; }
}
