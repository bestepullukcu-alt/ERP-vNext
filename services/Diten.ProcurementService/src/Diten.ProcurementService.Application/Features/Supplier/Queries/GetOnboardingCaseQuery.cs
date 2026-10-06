using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Queries;

/// <summary>getOnboardingCase (contract GET /{supplierId}/onboarding). Cross-tenant/LE → 404.</summary>
public sealed record GetOnboardingCaseQuery(string SupplierId) : IRequest<Response<OnboardingCaseDto>>;
