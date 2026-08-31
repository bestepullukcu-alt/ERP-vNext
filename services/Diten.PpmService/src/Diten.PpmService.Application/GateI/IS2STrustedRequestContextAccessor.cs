using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;


public interface IS2STrustedRequestContextAccessor
{
    S2STrustedRequestContext? Current { get; }
    void Publish(S2STrustedRequestContext context);
}
