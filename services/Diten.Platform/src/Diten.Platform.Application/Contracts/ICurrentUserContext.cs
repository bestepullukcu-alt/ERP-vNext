namespace Diten.Platform.Application.Contracts;

public interface ICurrentUserContext
{
    Guid UserId { get; }

    bool IsAuthenticated { get; }
}
