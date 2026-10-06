using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentScopes;

/// <summary>Lists content scopes for the tenant. Archived rows included by default so history stays visible.</summary>
public sealed record ListContentScopesQuery(
    string? Status = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<ContentScopeListDto>>;

public sealed record GetContentScopeQuery(Guid ContentScopeId) : IRequest<Response<ContentScopeDto>>;
