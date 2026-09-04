using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public static class DecisionTraceReadOnlyContract
{
    public const bool NonRuntimeContractOnly = true;
    public const bool RequiresIdempotencyKey = false;
    public const bool PersistsReceipt = false;
    public const bool PersistsAuditIntent = false;
    public const bool PersistsOutbox = false;
    public const bool PersistsCache = false;
    public const bool UsesLastKnownGoodAllow = false;
    public const bool AccessesProducerPersistence = false;
    public const bool MutatesInvestmentCase = false;
}
