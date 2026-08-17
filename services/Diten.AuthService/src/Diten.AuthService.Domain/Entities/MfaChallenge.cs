namespace Diten.AuthService.Domain.Entities;

public sealed class MfaChallenge : EntityBase
{
    private MfaChallenge() { }

    public MfaChallenge(
        Guid tenantId,
        Guid userId,
        string challengeIdHash,
        string channel,
        string destinationHash,
        string codeHash,
        DateTime expiresAtUtc,
        int maxAttempts,
        string createdIp,
        string? userAgent)
    {
        TenantId = tenantId;
        UserId = userId;
        ChallengeIdHash = challengeIdHash;
        Channel = channel;
        DestinationHash = destinationHash;
        CodeHash = codeHash;
        ExpiresAtUtc = expiresAtUtc;
        MaxAttempts = maxAttempts;
        CreatedIp = createdIp;
        UserAgent = userAgent;
    }

    public Guid UserId { get; private set; }
    public string ChallengeIdHash { get; private set; } = string.Empty;
    public string Channel { get; private set; } = "email";
    public string DestinationHash { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; } = 5;
    public int ResendCount { get; private set; }
    public DateTime? LastResentAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public string CreatedIp { get; private set; } = string.Empty;
    public string? UserAgent { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsConsumed => ConsumedAtUtc.HasValue;
    public bool HasAttemptsRemaining => AttemptCount < MaxAttempts;

    public void RecordFailedAttempt()
    {
        AttemptCount++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordResend(string codeHash, DateTime expiresAtUtc)
    {
        CodeHash = codeHash;
        ExpiresAtUtc = expiresAtUtc;
        AttemptCount = 0;
        ResendCount++;
        LastResentAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Consume()
    {
        ConsumedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
