namespace Diten.ProcurementService.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string UserName { get; }
}
