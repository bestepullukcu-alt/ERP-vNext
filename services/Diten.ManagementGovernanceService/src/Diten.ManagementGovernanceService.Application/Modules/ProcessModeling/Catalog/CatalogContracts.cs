using MediatR;

namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;

public sealed class CatalogResponse<T>
{
    public T? Data { get; private init; }
    public int StatusCode { get; private init; }
    public bool IsSuccessful { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];
    public static CatalogResponse<T> Success(T data, int statusCode = 200) => new() { Data = data, StatusCode = statusCode, IsSuccessful = true };
    public static CatalogResponse<T> Fail(string error, int statusCode) => new() { StatusCode = statusCode, Errors = [error] };
}

public static class CatalogErrors
{
    public const string InvalidRequest = "process_modeling_catalog_bad_request";
    public const string Unauthenticated = "process_modeling_catalog_unauthenticated";
    public const string PermissionDenied = "process_modeling_catalog_permission_denied";
    public const string NotFound = "process_modeling_catalog_not_found";
    public const string Conflict = "process_modeling_catalog_conflict";
    public const string Unavailable = "process_modeling_catalog_transaction_unavailable";
}

public sealed record CatalogCommandContext(Guid TenantId, Guid SubjectId, string IdempotencyKey, string Permission);
public sealed record CatalogQueryContext(Guid TenantId, Guid SubjectId, string Permission);
public sealed record CatalogMutationResult(Guid Id, int Version);

public enum CatalogMutationKind
{
    CreateArchitecture, UpdateArchitecture, ArchiveArchitecture,
    CreateDomain, UpdateDomain, ArchiveDomain,
    CreateFamily, UpdateFamily, ArchiveFamily,
    CreateDefinition, UpdateDefinition, ArchiveDefinition
}

public sealed record CatalogMutation(
    CatalogMutationKind Kind, Guid EntityId, Guid? ParentId, string? Code, string? Name,
    string? Purpose, string? Description, int? SortOrder, int? ExpectedVersion);

public sealed record ProcessDefinitionDto(Guid Id, Guid ProcessFamilyId, string ProcessCode, string Name, string? Purpose, string? Description, string LifecycleState, int Version);
public sealed record ProcessFamilyDto(Guid Id, Guid ProcessDomainId, string FamilyCode, string Name, string? Description, int SortOrder, string LifecycleState, int Version, IReadOnlyList<ProcessDefinitionDto> Definitions);
public sealed record ProcessDomainDto(Guid Id, Guid ProcessArchitectureId, string DomainCode, string Name, string? Description, int SortOrder, string LifecycleState, int Version, IReadOnlyList<ProcessFamilyDto> Families);
public sealed record ProcessArchitectureDto(Guid Id, string ArchitectureCode, string Name, string? Description, int SortOrder, string LifecycleState, int Version, IReadOnlyList<ProcessDomainDto> Domains);
public sealed record CatalogTreeDto(IReadOnlyList<ProcessArchitectureDto> Architectures);

public interface ICatalogStore
{
    Task<CatalogResponse<CatalogMutationResult>> MutateAsync(CatalogMutation mutation, CatalogCommandContext context, CancellationToken cancellationToken);
    Task<CatalogResponse<CatalogTreeDto>> GetTreeAsync(CatalogQueryContext context, CancellationToken cancellationToken);
    Task<CatalogResponse<ProcessDefinitionDto>> GetDefinitionAsync(Guid id, CatalogQueryContext context, CancellationToken cancellationToken);
}

public interface ICatalogCommand : IRequest<CatalogResponse<CatalogMutationResult>>
{
    CatalogMutation Mutation { get; }
    CatalogCommandContext Context { get; }
}

internal static class CatalogValidation
{
    public static string? Command(ICatalogCommand command)
    {
        var c = command.Context;
        var m = command.Mutation;
        if (c.TenantId == Guid.Empty || c.SubjectId == Guid.Empty || string.IsNullOrWhiteSpace(c.IdempotencyKey) || string.IsNullOrWhiteSpace(c.Permission)) return CatalogErrors.InvalidRequest;
        if (m.EntityId == Guid.Empty) return CatalogErrors.InvalidRequest;
        var create = m.Kind is CatalogMutationKind.CreateArchitecture or CatalogMutationKind.CreateDomain or CatalogMutationKind.CreateFamily or CatalogMutationKind.CreateDefinition;
        var archive = m.Kind is CatalogMutationKind.ArchiveArchitecture or CatalogMutationKind.ArchiveDomain or CatalogMutationKind.ArchiveFamily or CatalogMutationKind.ArchiveDefinition;
        if (!create && (!m.ExpectedVersion.HasValue || m.ExpectedVersion < 0)) return CatalogErrors.InvalidRequest;
        if (archive) return null;
        if (string.IsNullOrWhiteSpace(m.Name)) return CatalogErrors.InvalidRequest;
        if (create && string.IsNullOrWhiteSpace(m.Code)) return CatalogErrors.InvalidRequest;
        if (m.Kind is not (CatalogMutationKind.CreateDefinition or CatalogMutationKind.UpdateDefinition) && (!m.SortOrder.HasValue || m.SortOrder < 0)) return CatalogErrors.InvalidRequest;
        if (m.Kind is CatalogMutationKind.CreateDomain or CatalogMutationKind.CreateFamily or CatalogMutationKind.CreateDefinition
            && (m.ParentId is null || m.ParentId == Guid.Empty)) return CatalogErrors.InvalidRequest;
        return null;
    }

    public static string? Query(CatalogQueryContext context, Guid? id = null) =>
        context.TenantId == Guid.Empty || context.SubjectId == Guid.Empty || string.IsNullOrWhiteSpace(context.Permission) || id == Guid.Empty
            ? CatalogErrors.InvalidRequest : null;
}

internal abstract class CatalogCommandHandler<TCommand>(ICatalogStore store) : IRequestHandler<TCommand, CatalogResponse<CatalogMutationResult>> where TCommand : ICatalogCommand
{
    public Task<CatalogResponse<CatalogMutationResult>> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var error = CatalogValidation.Command(request);
        return error is null ? store.MutateAsync(request.Mutation, request.Context, cancellationToken) : Task.FromResult(CatalogResponse<CatalogMutationResult>.Fail(error, 400));
    }
}
