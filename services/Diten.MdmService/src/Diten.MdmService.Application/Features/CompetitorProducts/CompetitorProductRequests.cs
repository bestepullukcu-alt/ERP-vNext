using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.CompetitorProducts;

// WP-VW-W4 (W4-BE-b2) — the MediatR surface of the competitor products. Every WRITE is an audited command
// (IAuditableCommand + IAuditMetadataProvider → AuditForwardingBehavior → Platform's central audit store, the MDM
// LegalEntity pattern), so no write is left outside the audit trail (AUD-001). The logic is CompetitorProductService's.

internal static class CompetitorAudit
{
    public const string SourceModule = "competitor-products";

    public static AuditMetadata Of(AuditOperation operation, string entityType = "CompetitorProduct")
        => new(AuditCategory.MasterData, operation, entityType, SourceModule);
}

public sealed record ListCompetitorProductsQuery(
    Guid? ProductId = null, string? Company = null, string? Ingredient = null, string? Market = null, string? Reimbursement = null,
    string? Status = null, string? Q = null, string? IncludeProposed = null, int? Page = null, int? PageSize = null)
    : IRequest<Response<CompetitorListDto>>;

public sealed record GetCompetitorProductQuery(Guid Id) : IRequest<Response<CompetitorProductDto>>;

public sealed record LookupCompetitorProductsQuery(Guid? ProductId, Guid? IndicationNodeId, string? Market, string? Q)
    : IRequest<Response<IReadOnlyList<CompetitorLookupDto>>>;

public sealed record CompetitorMergeCandidatesQuery(Guid? SourceId, string? Q) : IRequest<Response<IReadOnlyList<CompetitorSimilarDto>>>;

public sealed record GetCompetitorUsageQuery(Guid Id) : IRequest<Response<CompetitorUsageDto>>;

public sealed record ListCompetitorProposalsQuery : IRequest<Response<IReadOnlyList<CompetitorProposalDto>>>;

public sealed record GetCompetitorProposalQuery(Guid ProposalId) : IRequest<Response<CompetitorProposalDto>>;

public sealed record CreateCompetitorProductCommand(CompetitorProductWriteRequest Request)
    : IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => CompetitorAudit.Of(AuditOperation.Create);
}

public sealed record UpdateCompetitorProductCommand(Guid CompetitorProductId, CompetitorProductWriteRequest Request)
    : IRequest<Response<CompetitorProductDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => CompetitorAudit.Of(AuditOperation.Update);
}

public sealed record DeactivateCompetitorProductCommand(Guid CompetitorProductId, int? ExpectedVersion)
    : IRequest<Response<CompetitorProductDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => CompetitorAudit.Of(AuditOperation.Deactivate);
}

public sealed record MergeCompetitorProductCommand(Guid CompetitorProductId, CompetitorMergeRequest Request)
    : IRequest<Response<CompetitorProductDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => CompetitorAudit.Of(AuditOperation.Update);
}

public sealed record ProposeCompetitorProductCommand(CompetitorProposalRequest Request)
    : IRequest<Response<CompetitorProposalResult>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => CompetitorAudit.Of(AuditOperation.Create);
}

public sealed record SaveCompetitorDecisionCommand(Guid ProposalId, CompetitorDecisionRequest Request)
    : IRequest<Response<CompetitorDecisionDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => CompetitorAudit.Of(AuditOperation.Update, "CompetitorProposalDecision");
}

public sealed record SubmitCompetitorDecisionCommand(Guid ProposalId)
    : IRequest<Response<CompetitorDecisionDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => CompetitorAudit.Of(AuditOperation.Execute, "CompetitorProposalDecision");
}

public sealed record WithdrawCompetitorDecisionCommand(Guid ProposalId)
    : IRequest<Response<CompetitorDecisionDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => CompetitorAudit.Of(AuditOperation.Revoke, "CompetitorProposalDecision");
}

public sealed record ReviewCompetitorDecisionCommand(Guid ProposalId, CompetitorReviewDecisionRequest Request)
    : IRequest<Response<CompetitorDecisionDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => CompetitorAudit.Of(AuditOperation.Execute, "CompetitorProposalDecision");
}

/// <summary>One handler for the whole surface: each request is a thin call onto <see cref="CompetitorProductService"/>.</summary>
public sealed class CompetitorProductHandlers :
    IRequestHandler<ListCompetitorProductsQuery, Response<CompetitorListDto>>,
    IRequestHandler<GetCompetitorProductQuery, Response<CompetitorProductDto>>,
    IRequestHandler<LookupCompetitorProductsQuery, Response<IReadOnlyList<CompetitorLookupDto>>>,
    IRequestHandler<CompetitorMergeCandidatesQuery, Response<IReadOnlyList<CompetitorSimilarDto>>>,
    IRequestHandler<GetCompetitorUsageQuery, Response<CompetitorUsageDto>>,
    IRequestHandler<ListCompetitorProposalsQuery, Response<IReadOnlyList<CompetitorProposalDto>>>,
    IRequestHandler<GetCompetitorProposalQuery, Response<CompetitorProposalDto>>,
    IRequestHandler<CreateCompetitorProductCommand, Response<Guid>>,
    IRequestHandler<UpdateCompetitorProductCommand, Response<CompetitorProductDto>>,
    IRequestHandler<DeactivateCompetitorProductCommand, Response<CompetitorProductDto>>,
    IRequestHandler<MergeCompetitorProductCommand, Response<CompetitorProductDto>>,
    IRequestHandler<ProposeCompetitorProductCommand, Response<CompetitorProposalResult>>,
    IRequestHandler<SaveCompetitorDecisionCommand, Response<CompetitorDecisionDto>>,
    IRequestHandler<SubmitCompetitorDecisionCommand, Response<CompetitorDecisionDto>>,
    IRequestHandler<WithdrawCompetitorDecisionCommand, Response<CompetitorDecisionDto>>,
    IRequestHandler<ReviewCompetitorDecisionCommand, Response<CompetitorDecisionDto>>
{
    private readonly CompetitorProductService _service;

    public CompetitorProductHandlers(CompetitorProductService service) => _service = service;

    public Task<Response<CompetitorListDto>> Handle(ListCompetitorProductsQuery r, CancellationToken ct)
        => _service.ListAsync(r.ProductId, r.Company, r.Ingredient, r.Market, r.Reimbursement, r.Status, r.Q, r.IncludeProposed, r.Page, r.PageSize, ct);

    public Task<Response<CompetitorProductDto>> Handle(GetCompetitorProductQuery r, CancellationToken ct) => _service.GetAsync(r.Id, ct);

    public Task<Response<IReadOnlyList<CompetitorLookupDto>>> Handle(LookupCompetitorProductsQuery r, CancellationToken ct)
        => _service.LookupAsync(r.ProductId, r.IndicationNodeId, r.Market, r.Q, ct);

    public Task<Response<IReadOnlyList<CompetitorSimilarDto>>> Handle(CompetitorMergeCandidatesQuery r, CancellationToken ct)
        => _service.MergeCandidatesAsync(r.SourceId, r.Q, ct);

    public Task<Response<CompetitorUsageDto>> Handle(GetCompetitorUsageQuery r, CancellationToken ct) => _service.UsageAsync(r.Id, ct);

    public Task<Response<IReadOnlyList<CompetitorProposalDto>>> Handle(ListCompetitorProposalsQuery r, CancellationToken ct) => _service.ProposalsAsync(ct);

    public Task<Response<CompetitorProposalDto>> Handle(GetCompetitorProposalQuery r, CancellationToken ct) => _service.ProposalAsync(r.ProposalId, ct);

    public Task<Response<Guid>> Handle(CreateCompetitorProductCommand r, CancellationToken ct) => _service.CreateAsync(r.Request, ct);

    public Task<Response<CompetitorProductDto>> Handle(UpdateCompetitorProductCommand r, CancellationToken ct)
        => _service.UpdateAsync(r.CompetitorProductId, r.Request, ct);

    public Task<Response<CompetitorProductDto>> Handle(DeactivateCompetitorProductCommand r, CancellationToken ct)
        => _service.DeactivateAsync(r.CompetitorProductId, r.ExpectedVersion, ct);

    public Task<Response<CompetitorProductDto>> Handle(MergeCompetitorProductCommand r, CancellationToken ct)
        => _service.MergeAsync(r.CompetitorProductId, r.Request, ct);

    public Task<Response<CompetitorProposalResult>> Handle(ProposeCompetitorProductCommand r, CancellationToken ct) => _service.ProposeAsync(r.Request, ct);

    public Task<Response<CompetitorDecisionDto>> Handle(SaveCompetitorDecisionCommand r, CancellationToken ct)
        => _service.SaveDecisionAsync(r.ProposalId, r.Request, ct);

    public Task<Response<CompetitorDecisionDto>> Handle(SubmitCompetitorDecisionCommand r, CancellationToken ct) => _service.SubmitDecisionAsync(r.ProposalId, ct);

    public Task<Response<CompetitorDecisionDto>> Handle(WithdrawCompetitorDecisionCommand r, CancellationToken ct) => _service.WithdrawDecisionAsync(r.ProposalId, ct);

    public Task<Response<CompetitorDecisionDto>> Handle(ReviewCompetitorDecisionCommand r, CancellationToken ct)
        => _service.ReviewDecisionAsync(r.ProposalId, r.Request, ct);
}
