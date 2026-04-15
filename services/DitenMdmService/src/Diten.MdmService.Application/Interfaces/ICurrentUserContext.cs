namespace Diten.MdmService.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string UserName { get; }
}
