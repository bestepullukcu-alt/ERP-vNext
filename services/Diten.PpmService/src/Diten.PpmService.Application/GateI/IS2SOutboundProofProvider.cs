using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;


public interface IS2SOutboundProofProvider
{
    bool IsAvailable { get; }

    ValueTask<S2SOutboundProofResult> IssueAsync(
        S2SOutboundProofRequest request,
        CancellationToken cancellationToken);
}
