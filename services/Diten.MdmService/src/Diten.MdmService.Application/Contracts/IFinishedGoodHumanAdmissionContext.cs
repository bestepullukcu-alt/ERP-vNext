namespace Diten.MdmService.Application.Contracts;

public interface IFinishedGoodHumanAdmissionContext
{
    bool TryResolveSubmitter(out Guid tenantId, out Guid subjectId);
}
