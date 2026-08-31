using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;


public sealed record S2SOutboundReceiverProfile(
    string OwnerModule,
    string Method,
    string Path,
    string Audience,
    string ClientId,
    string Operation,
    string Permission);
