using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Features.Dws;

public interface IDwsFunctionalValidator<in TRequest>
{
    void Validate(TRequest request);
}

public interface IFu16DwsFunctionalAuthorization
{
    Task<DwsFu16AuthorizationSnapshot> AuthorizeAsync(
        DwsTrustedActorContext context,
        string moduleCode,
        string moduleEntitlementCode,
        string operation,
        string permission,
        CancellationToken cancellationToken);

    Task RevalidateAsync(
        DwsTrustedActorContext context,
        DwsFu16AuthorizationSnapshot snapshot,
        CancellationToken cancellationToken);
}

public static class DwsFunctionalAuthorizationBinding
{
    public const string ModuleCode = "MOD-0354";
    public const string ModuleEntitlementCode = "MOD-0354";
}

public sealed record DwsFu16AuthorizationSnapshot(
    Guid TenantId,
    Guid SecuritySubjectId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId,
    string ModuleCode,
    string ModuleEntitlementCode,
    string Operation,
    string Permission,
    bool HasExplicitTenantGrant,
    long PrincipalGeneration,
    long CredentialGeneration,
    long AuthorizationVersion,
    long EntitlementVersion);

public interface IMod0117DwsContextValidator
{
    Task<DwsMod0117ContextSnapshot> ValidateAsync(
        DwsTrustedActorContext context,
        ExternalContextReference reference,
        CancellationToken cancellationToken);

    Task RevalidateAsync(
        DwsTrustedActorContext context,
        ExternalContextReference reference,
        DwsMod0117ContextSnapshot snapshot,
        CancellationToken cancellationToken);
}

public sealed record DwsStructureVisibilitySnapshot(
    Guid TenantId,
    Guid StructureDefinitionId,
    int DefinitionVersion,
    ExternalContextReference ExternalContextReference);

public interface IDwsStructureVisibilityPort
{
    Task<DwsStructureVisibilitySnapshot> CaptureAsync(
        Guid structureDefinitionId,
        DwsTrustedActorContext context,
        CancellationToken cancellationToken);

    Task RevalidateAsync(
        DwsTrustedActorContext context,
        DwsStructureVisibilitySnapshot snapshot,
        CancellationToken cancellationToken);
}

internal sealed record DwsExistingStructureSecuritySnapshot(
    DwsStructureVisibilitySnapshot Visibility,
    DwsMod0117ContextSnapshot ExternalContext,
    DwsFu16AuthorizationSnapshot Authorization);

internal static class DwsExistingStructureSecurity
{
    public static async Task<DwsExistingStructureSecuritySnapshot> CaptureAsync(
        Guid structureDefinitionId,
        DwsTrustedActorContext context,
        string operation,
        string permission,
        IFu16DwsFunctionalAuthorization authorization,
        IMod0117DwsContextValidator contexts,
        IDwsStructureVisibilityPort visibility,
        CancellationToken cancellationToken)
    {
        var authorizationSnapshot = await authorization.AuthorizeAsync(
            context,
            DwsFunctionalAuthorizationBinding.ModuleCode,
            DwsFunctionalAuthorizationBinding.ModuleEntitlementCode,
            operation,
            permission,
            cancellationToken);
        var visibilitySnapshot = await visibility.CaptureAsync(structureDefinitionId, context, cancellationToken);
        var contextSnapshot = await contexts.ValidateAsync(
            context,
            visibilitySnapshot.ExternalContextReference,
            cancellationToken);
        return new(visibilitySnapshot, contextSnapshot, authorizationSnapshot);
    }

    public static async Task RevalidateAsync(
        DwsTrustedActorContext context,
        DwsExistingStructureSecuritySnapshot snapshot,
        IFu16DwsFunctionalAuthorization authorization,
        IMod0117DwsContextValidator contexts,
        IDwsStructureVisibilityPort visibility,
        CancellationToken cancellationToken)
    {
        await visibility.RevalidateAsync(context, snapshot.Visibility, cancellationToken);
        await contexts.RevalidateAsync(
            context,
            snapshot.Visibility.ExternalContextReference,
            snapshot.ExternalContext,
            cancellationToken);
        await authorization.RevalidateAsync(context, snapshot.Authorization, cancellationToken);
    }
}

public sealed record DwsMod0117ContextSnapshot(
    Guid TenantId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId,
    ExternalContextReference Reference,
    long AuthorityFence);

public interface IDwsFunctionalCommandPort
{
    Task<CreateStructureResult> CreateStructureAsync(CreateStructureRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<UpdateStructureMetadataResult> UpdateStructureMetadataAsync(UpdateStructureMetadataRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<AddStructureNodeResult> AddStructureNodeAsync(AddStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<MoveStructureNodeResult> MoveStructureNodeAsync(MoveStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<ReorderStructureNodeResult> ReorderStructureNodeAsync(ReorderStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<RemoveStructureNodeResult> RemoveStructureNodeAsync(RemoveStructureNodeRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<AddStructuralDependencyResult> AddStructuralDependencyAsync(AddStructuralDependencyRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<RemoveStructuralDependencyResult> RemoveStructuralDependencyAsync(RemoveStructuralDependencyRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<CreateStructureBaselineResult> CreateStructureBaselineAsync(CreateStructureBaselineRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<CreateNextStructureRevisionResult> CreateNextStructureRevisionAsync(CreateNextStructureRevisionRequest request, DwsTrustedActorContext context, CancellationToken cancellationToken);
}

public interface IDwsFunctionalQueryPort
{
    Task<StructureSummaryDto> GetStructureByIdAsync(Guid structureDefinitionId, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<StructureTreeDto> GetStructureTreeAsync(Guid structureDefinitionId, int? revisionNumber, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<StructureValidationDto> ValidateStructureAsync(Guid structureDefinitionId, int? revisionNumber, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<StructureComparisonDto> CompareStructureRevisionsAsync(Guid structureDefinitionId, int leftRevisionNumber, int rightRevisionNumber, DwsTrustedActorContext context, CancellationToken cancellationToken);
    Task<BaselineComparisonDto> CompareStructureBaselinesAsync(Guid structureDefinitionId, int leftBaselineNumber, int rightBaselineNumber, DwsTrustedActorContext context, CancellationToken cancellationToken);
}

internal static class DwsFunctionalResponse
{
    public static async Task<Response<T>> ExecuteAsync<T>(Func<Task<T>> action, int successStatus = 200)
    {
        try
        {
            return Response<T>.Success(await action(), successStatus);
        }
        catch (DwsNotFoundException error)
        {
            return Response<T>.Fail(error.Code, 404);
        }
        catch (DwsConflictException error)
        {
            return Response<T>.Fail(error.Code, 409);
        }
        catch (DwsValidationException error)
        {
            var status = DwsErrors.Matrix.SingleOrDefault(entry => entry.Value.Contains(error.Code)).Key;
            return Response<T>.Fail(error.Code, status == 0 ? 400 : status);
        }
        catch (InvalidOperationException error) when (Status(error.Message) is int status)
        {
            return Response<T>.Fail(error.Message, status);
        }
    }

    private static int? Status(string code)
    {
        var match = DwsErrors.Matrix.SingleOrDefault(entry => entry.Value.Contains(code));
        return match.Value is null ? null : match.Key;
    }
}

internal static class DwsFunctionalValidation
{
    public static void Identity(Guid value)
    {
        if (value == Guid.Empty) throw new DwsValidationException(DwsErrors.InvalidRequest);
    }

    public static void IdentityVersion(Guid value, int version)
    {
        Identity(value);
        if (version <= 0) throw new DwsValidationException(DwsErrors.InvalidRequest);
    }

    public static void OptionalPositive(int? value)
    {
        if (value is <= 0) throw new DwsValidationException(DwsErrors.InvalidRequest);
    }

    public static void PositiveDistinctPair(int left, int right)
    {
        if (left <= 0 || right <= 0 || left == right) throw new DwsValidationException(DwsErrors.InvalidRequest);
    }

    public static void Dependency(Guid definitionId, Guid from, Guid to, int version)
    {
        IdentityVersion(definitionId, version);
        Identity(from);
        Identity(to);
        if (from == to) throw new DwsValidationException(DwsErrors.InvalidStructure);
    }
}
