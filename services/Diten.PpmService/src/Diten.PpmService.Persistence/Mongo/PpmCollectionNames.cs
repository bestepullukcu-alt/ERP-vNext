using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.BuildingBlocks.Eventing;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Mongo;


public static class PpmCollectionNames
{
    public const string Portfolios = "ppm_portfolios";
    public const string Initiatives = "ppm_initiatives";
    public const string Programs = "ppm_programs";
    public const string Projects = "ppm_projects";
    public const string InvestmentCases = "ppm_investment_cases";
    public const string BenefitCommitments = "ppm_benefit_commitments";
    public const string AuditIntents = "ppm_audit_intents";
    public const string EventOutbox = "ppm_event_outbox";
    public const string GateIMutationReceipts = "ppm_gate_i_mutation_receipts";
}
