using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.DocumentManagementContract.Queries;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementContract.Handlers.QueryHandlers;

public sealed class GetDocumentManagementContractHandler
    : IRequestHandler<GetDocumentManagementContractQuery, Response<DocumentManagementContractResponse>>
{
    private const string Warning = "This endpoint does not indicate full MOD-0028 business readiness.";
    private readonly DocumentManagementFeatureFlagOptions _featureFlags;
    private readonly IServiceProviderIsService _serviceProbe;

    public GetDocumentManagementContractHandler(
        IOptions<DocumentManagementFeatureFlagOptions> featureFlags,
        IServiceProviderIsService serviceProbe)
    {
        _featureFlags = featureFlags.Value;
        _serviceProbe = serviceProbe;
    }

    public Task<Response<DocumentManagementContractResponse>> Handle(
        GetDocumentManagementContractQuery request,
        CancellationToken cancellationToken)
    {
        var data = new DocumentManagementContractResponse(
            "MOD-0028-FU01",
            "Documentation Management Backend Contract Foundation",
            "MOD-0028",
            DocumentManagementRoutes.ApiFamily,
            "1.0",
            ["CORPORATE", "COMPANY"],
            ["POSITION", "PERSON"],
            _featureFlags.ToSummary(),
            DocumentManagementPermissions.RequiredForFu01,
            _serviceProbe.IsService(typeof(ILegalEntityReferenceValidator)),
            _serviceProbe.IsService(typeof(IAuditService)) && !string.IsNullOrWhiteSpace(request.CorrelationId),
            "FOUNDATION_ONLY",
            Warning);

        return Task.FromResult(Response<DocumentManagementContractResponse>.Success(
            data,
            correlationId: request.CorrelationId));
    }
}
