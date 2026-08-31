using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;


public enum S2SOutboundProofDisposition
{
    Issued,
    Unauthenticated,
    Forbidden,
    Conflict,
    Unavailable
}
