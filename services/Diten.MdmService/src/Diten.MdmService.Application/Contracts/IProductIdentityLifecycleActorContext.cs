namespace Diten.MdmService.Application.Contracts;

public interface IProductIdentityLifecycleActorContext
{
    bool TryResolveCanonicalHumanSubject(out Guid subjectId);
    bool HasPermission(string permission);
}
