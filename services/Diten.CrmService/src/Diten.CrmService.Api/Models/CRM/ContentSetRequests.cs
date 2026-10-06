namespace Diten.CrmService.Api.Models.CRM;

// SCMM-14 (CAND-CAP-0011) content-set request models. TenantId is NEVER in a request body (server-resolved). Route ids
// (contentSetId / selectionId) come from the path. Remove and apply-eligibility carry only route ids, so they need no body.

public sealed record CreateContentSetDraftRequest(
    string SetCode,
    string SetName,
    Guid ConceptChainTemplateId,
    string? Description = null,
    Guid? ContentScopeId = null);

public sealed record CloneContentSetRequest(string NewSetCode, string? NewSetName = null);

public sealed record UpdateContentSetRequest(string SetName, string? Description = null, string? Status = null);

public sealed record AddContentSetComponentRequest(
    Guid KnowledgeContentId,
    Guid TemplateStepId,
    int Position,
    string? BranchId = null,
    string? Role = null);

public sealed record ArrangeContentSetComponentRequest(
    Guid TemplateStepId,
    int Position,
    string? BranchId = null);

public sealed record AddContentSetClaimRequest(
    Guid ClaimId,
    Guid TemplateStepId,
    int Position,
    string? BranchId = null);

public sealed record ArrangeContentSetClaimRequest(
    Guid TemplateStepId,
    int Position,
    string? BranchId = null);
