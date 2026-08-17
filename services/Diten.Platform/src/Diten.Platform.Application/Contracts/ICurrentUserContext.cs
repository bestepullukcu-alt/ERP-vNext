namespace Diten.Platform.Application.Contracts;

public interface ICurrentUserContext
{
    Guid UserId { get; }

    string? Email { get; }

    string? DisplayName { get; }

    string ActorName { get; }

    bool IsAuthenticated { get; }
}
