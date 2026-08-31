using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.BenefitRealization;
using Diten.PpmService.Domain.GateI.DecisionTrace;
using Diten.PpmService.Domain.GateI.FundingScenario;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public sealed class GateIRelationshipUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
