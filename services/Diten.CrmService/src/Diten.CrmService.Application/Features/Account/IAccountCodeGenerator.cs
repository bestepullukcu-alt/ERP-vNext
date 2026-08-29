namespace Diten.CrmService.Application.Features.Account;

public interface IAccountCodeGenerator
{
    /// <summary>Generates a tenant+year scoped unique AccountCode in the form ACC-{YYYY}-{sequence:000000}.
    /// Retries on collision; throws <see cref="AccountCodeGenerationException"/> when retries are exhausted.</summary>
    Task<string> GenerateAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>Controlled application error raised when a unique AccountCode cannot be generated within the retry budget.</summary>
public sealed class AccountCodeGenerationException : Exception
{
    public AccountCodeGenerationException(string message) : base(message)
    {
    }
}
