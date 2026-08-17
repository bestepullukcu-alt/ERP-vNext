namespace Diten.DevEnablementService.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string UserName { get; }
}
