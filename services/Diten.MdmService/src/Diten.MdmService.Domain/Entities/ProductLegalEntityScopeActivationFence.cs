using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class ProductLegalEntityScopeActivationFence
{
    public string Token { get; set; } = string.Empty;
    public ProductLegalEntityScopeAdmissionState State { get; set; } = ProductLegalEntityScopeAdmissionState.Closing;
    public string Action { get; set; } = string.Empty;
    public Guid CommandId { get; set; }
    public Guid ActorId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public DateTimeOffset AcquiredAtUtc { get; set; }
    public string? FirstStableFactsHash { get; set; }
    public string? SecondStableFactsHash { get; set; }

    public void EnsureValid()
    {
        if (!Hex(Token) || !Enum.IsDefined(State) || CommandId == Guid.Empty || ActorId == Guid.Empty
            || Action is not ("ActivateEnforced" or "SuspendFailClosed")
            || !Reason(ReasonCode) || AcquiredAtUtc.Offset != TimeSpan.Zero
            || FirstStableFactsHash is not null && !Hex(FirstStableFactsHash)
            || SecondStableFactsHash is not null && !Hex(SecondStableFactsHash))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_ACTIVATION_FENCE_INVALID");
        }
    }

    public static bool Reason(string value) => value.Length is >= 1 and <= 64
        && value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-')
        && value[0] is >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool Hex(string value) => value.Length == 64 && value.All(Uri.IsHexDigit)
        && string.Equals(value, value.ToUpperInvariant(), StringComparison.Ordinal);
}
