using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IS2SReplayReceiptStore
{
    Task<ReplayReceiptAcceptance> TryAcceptAsync(S2SReplayReceipt receipt, CancellationToken cancellationToken);
}
