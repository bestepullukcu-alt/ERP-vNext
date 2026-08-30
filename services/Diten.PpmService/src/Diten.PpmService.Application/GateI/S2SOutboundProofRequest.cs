using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;


public sealed record S2SOutboundProofRequest(
    S2SOutboundReceiverProfile Receiver,
    S2SOutboundTrustedContext TrustedContext,
    ReadOnlyMemory<byte> RawBody,
    string RequestHash);
