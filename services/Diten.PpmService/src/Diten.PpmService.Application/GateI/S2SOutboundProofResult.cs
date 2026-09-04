using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;


public sealed record S2SOutboundProofResult(
    S2SOutboundProofDisposition Disposition,
    IS2SOutboundProof? Proof = null,
    string? StableCode = null);
