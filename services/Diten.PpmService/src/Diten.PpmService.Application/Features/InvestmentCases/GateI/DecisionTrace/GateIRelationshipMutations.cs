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

public enum GateIRelationshipKind
{
    GoverningDecision,
    SupportingDecision,
    SelectedBudgetVersion,
    ScenarioVersion,
    ComparatorOutput,
    SelectedScenario,
    BenefitOutcome
}

public enum GateIRelationshipAction { AttachOrReplace, Remove }

public sealed record GateIRelationshipMutationCommand(
    Guid AggregateId,
    GateIRelationshipKind Kind,
    GateIRelationshipAction Action,
    byte[] CanonicalWrapperUtf8,
    Guid? ReferenceId,
    int ExpectedVersion,
    string IdempotencyKey,
    string OperationId) : IRequest<Response<GateIRelationshipMutationResult>>;

public sealed record GateIRelationshipMutationResult(
    Guid AggregateId,
    int Version,
    string StableCode,
    bool Replayed);

public sealed record GateIAuthorityValidationRequest(
    GateIRelationshipKind Kind,
    GateIRelationshipAction Action,
    Guid TenantId,
    Guid ActorId,
    string OperationId,
    ReadOnlyMemory<byte> CanonicalWrapperUtf8)
{
    public Fu16OutboundReceiverProfile Receiver => Kind switch
    {
        GateIRelationshipKind.GoverningDecision or GateIRelationshipKind.SupportingDecision =>
            Fu16OutboundReceiverProfiles.DecisionRegistry,
        GateIRelationshipKind.SelectedBudgetVersion => Fu16OutboundReceiverProfiles.Budgeting,
        GateIRelationshipKind.ScenarioVersion or GateIRelationshipKind.ComparatorOutput
            or GateIRelationshipKind.SelectedScenario => Fu16OutboundReceiverProfiles.ScenarioPlanning,
        GateIRelationshipKind.BenefitOutcome => Fu16OutboundReceiverProfiles.OutcomeTracking,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
}

public sealed record GateIAuthorityValidationResult(
    int StatusCode,
    string StableCode,
    string ProvenanceHash,
    bool Accepted);

public interface IGateIRelationshipAuthority
{
    Task<GateIAuthorityValidationResult> ValidateAsync(
        GateIAuthorityValidationRequest request,
        CancellationToken cancellationToken);
}

public sealed record GateIMutationScope(
    Guid TenantId,
    Guid ActorId,
    Guid CorrelationId,
    string OperationId,
    string IdempotencyKey,
    string RequestHash,
    string ProvenanceHash);

public enum GateIReceiptDisposition { Missing, Matching, Conflict }

public sealed record GateIReceiptResult(
    GateIReceiptDisposition Disposition,
    GateIRelationshipMutationResult? StoredResult = null);

public interface IGateIRelationshipMutationPersistence
{
    Task<GateIReceiptResult> ReconcileAsync(
        GateIMutationScope scope,
        CancellationToken cancellationToken);

    Task<GateIRelationshipMutationResult> ExecuteInvestmentCaseAsync(
        GateIMutationScope scope,
        Guid aggregateId,
        int expectedVersion,
        Action<InvestmentCase> mutation,
        string mutationName,
        CancellationToken cancellationToken);

    Task<GateIRelationshipMutationResult> ExecuteBenefitCommitmentAsync(
        GateIMutationScope scope,
        Guid aggregateId,
        int expectedVersion,
        Action<BenefitCommitment> mutation,
        string mutationName,
        CancellationToken cancellationToken);
}

public sealed class GateIRelationshipMutationHandler(
    IGateIRelationshipAuthority authority,
    IGateIRelationshipMutationPersistence persistence,
    IPpmAccessAuthorizer access,
    ITenantContext tenant,
    ICurrentActorContext actor,
    ICorrelationContext correlation)
    : IRequestHandler<GateIRelationshipMutationCommand, Response<GateIRelationshipMutationResult>>
{
    public async Task<Response<GateIRelationshipMutationResult>> Handle(
        GateIRelationshipMutationCommand request,
        CancellationToken cancellationToken)
    {
        if (!Valid(request))
            return Response<GateIRelationshipMutationResult>.Fail("Malformed Gate I mutation request.", 400);

        var permission = request.Kind == GateIRelationshipKind.BenefitOutcome
            ? PpmPermissions.BenefitCommitmentsUpdate
            : PpmPermissions.InvestmentCasesUpdate;
        var accessDecision = await access.AuthorizeAsync(permission, cancellationToken);
        if (accessDecision != PpmAccessDecision.Allowed)
            return accessDecision.Failure<GateIRelationshipMutationResult>();

        if (tenant.TenantId == Guid.Empty || actor.ActorId == Guid.Empty)
            return Response<GateIRelationshipMutationResult>.Fail("Authentication context is invalid.", 401);

        var requestHash = HashRequest(request, tenant.TenantId, actor.ActorId);
        var preliminary = new GateIMutationScope(
            tenant.TenantId, actor.ActorId, correlation.CorrelationId,
            request.OperationId, request.IdempotencyKey, requestHash, string.Empty);
        var receipt = await persistence.ReconcileAsync(preliminary, cancellationToken);
        if (receipt.Disposition == GateIReceiptDisposition.Conflict)
            return Response<GateIRelationshipMutationResult>.Fail("Idempotency key payload conflict.", 409);
        if (receipt is { Disposition: GateIReceiptDisposition.Matching, StoredResult: not null })
            return Response<GateIRelationshipMutationResult>.Success(receipt.StoredResult with { Replayed = true });

        GateIAuthorityValidationResult authorityResult;
        if (request.Action == GateIRelationshipAction.Remove)
        {
            authorityResult = new GateIAuthorityValidationResult(
                200, "ppm_gate_i_local_relationship_removal", HashText("ppm-owned-removal-v1"), true);
        }
        else
        {
            authorityResult = await authority.ValidateAsync(
                new GateIAuthorityValidationRequest(
                    request.Kind, request.Action, tenant.TenantId, actor.ActorId,
                    request.OperationId, request.CanonicalWrapperUtf8),
                cancellationToken);
        }

        if (!authorityResult.Accepted)
            return Response<GateIRelationshipMutationResult>.Fail(
                authorityResult.StableCode,
                authorityResult.StatusCode);
        if (!IsLowerHex64(authorityResult.ProvenanceHash))
            return Response<GateIRelationshipMutationResult>.Fail("Authoritative provenance is malformed.", 503);

        var scope = preliminary with { ProvenanceHash = authorityResult.ProvenanceHash };
        try
        {
            GateIRelationshipMutationResult result;
            if (request.Kind == GateIRelationshipKind.BenefitOutcome)
            {
                var mutation = BuildBenefitMutation(request, actor.ActorId);
                result = await persistence.ExecuteBenefitCommitmentAsync(
                    scope, request.AggregateId, request.ExpectedVersion,
                    mutation, MutationName(request), cancellationToken);
            }
            else
            {
                var mutation = BuildInvestmentMutation(request, actor.ActorId);
                result = await persistence.ExecuteInvestmentCaseAsync(
                    scope, request.AggregateId, request.ExpectedVersion,
                    mutation, MutationName(request), cancellationToken);
            }
            return Response<GateIRelationshipMutationResult>.Success(result);
        }
        catch (DecisionTraceContractException exception)
        {
            return Response<GateIRelationshipMutationResult>.Fail(exception.Message, 400);
        }
        catch (ArgumentException exception)
        {
            return Response<GateIRelationshipMutationResult>.Fail(exception.Message, 400);
        }
        catch (FormatException exception)
        {
            return Response<GateIRelationshipMutationResult>.Fail(exception.Message, 400);
        }
        catch (GateIRelationshipNotFoundException)
        {
            return Response<GateIRelationshipMutationResult>.Fail("Gate I aggregate was not found.", 404);
        }
        catch (GateIRelationshipConflictException exception)
        {
            return Response<GateIRelationshipMutationResult>.Fail(exception.Message, 409);
        }
        catch (GateIRelationshipUnavailableException)
        {
            return Response<GateIRelationshipMutationResult>.Fail("Gate I persistence is unavailable.", 503);
        }
    }

    private static Action<InvestmentCase> BuildInvestmentMutation(
        GateIRelationshipMutationCommand request,
        Guid actorId)
    {
        if (request.Action == GateIRelationshipAction.Remove)
        {
            return request.Kind switch
            {
                GateIRelationshipKind.GoverningDecision => entity => entity.RemoveGoverningDecision(actorId),
                GateIRelationshipKind.SupportingDecision => entity => entity.RemoveSupportingDecision(actorId, request.ReferenceId!.Value),
                GateIRelationshipKind.SelectedBudgetVersion => entity => entity.RemoveSelectedBudget(actorId),
                GateIRelationshipKind.ScenarioVersion => entity => entity.RemoveScenarioVersion(actorId, request.ReferenceId!.Value),
                GateIRelationshipKind.ComparatorOutput => entity => entity.RemoveComparatorOutput(actorId, request.ReferenceId!.Value),
                GateIRelationshipKind.SelectedScenario => entity => entity.RemoveSelectedScenario(actorId),
                _ => throw new ArgumentOutOfRangeException(nameof(request.Kind))
            };
        }

        var json = Encoding.UTF8.GetString(request.CanonicalWrapperUtf8);
        switch (request.Kind)
        {
            case GateIRelationshipKind.GoverningDecision:
            {
                var reference = (GoverningDecisionReferenceV1)DecisionTraceReferenceCodec.Parse(request.CanonicalWrapperUtf8);
                return entity => entity.SetGoverningDecision(actorId, reference);
            }
            case GateIRelationshipKind.SupportingDecision:
            {
                var reference = (SupportingDecisionReferenceV1)DecisionTraceReferenceCodec.Parse(request.CanonicalWrapperUtf8);
                return entity => entity.AddSupportingDecision(actorId, reference);
            }
            case GateIRelationshipKind.SelectedBudgetVersion:
            {
                var reference = SelectedBudgetVersionReferenceV1.ParseExact(json);
                return entity => entity.SetSelectedBudget(actorId, reference);
            }
            case GateIRelationshipKind.ScenarioVersion:
            {
                var reference = InvestmentCaseScenarioVersionReferenceV1.ParseExact(json);
                return entity => entity.AddScenarioVersion(actorId, reference);
            }
            case GateIRelationshipKind.ComparatorOutput:
            {
                var reference = InvestmentCaseComparatorOutputReferenceV1.ParseExact(json);
                return entity => entity.AddComparatorOutput(actorId, reference);
            }
            case GateIRelationshipKind.SelectedScenario:
            {
                var reference = SelectedScenarioReferenceV1.ParseExact(json);
                return entity => entity.SetSelectedScenario(actorId, reference);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Kind));
        }
    }

    private static Action<BenefitCommitment> BuildBenefitMutation(
        GateIRelationshipMutationCommand request,
        Guid actorId)
    {
        if (request.Action == GateIRelationshipAction.Remove)
            return entity => entity.RemoveOutcomeReference(actorId, request.ReferenceId!.Value);
        var reference = BenefitCommitmentOutcomeReferenceV1Codec.ParseStrict(request.CanonicalWrapperUtf8);
        return entity => entity.AddOutcomeReference(actorId, reference);
    }

    private static bool Valid(GateIRelationshipMutationCommand request) =>
        request.AggregateId != Guid.Empty
        && request.ExpectedVersion > 0
        && Enum.IsDefined(request.Kind)
        && Enum.IsDefined(request.Action)
        && !string.IsNullOrWhiteSpace(request.OperationId)
        && request.OperationId.Length <= 128
        && request.OperationId.All(character => character is >= 'a' and <= 'z' || char.IsDigit(character) || character is '.' or '-')
        && !string.IsNullOrWhiteSpace(request.IdempotencyKey)
        && request.IdempotencyKey.Length <= 128
        && (request.Action == GateIRelationshipAction.AttachOrReplace
            ? request.CanonicalWrapperUtf8 is { Length: > 0 and <= 8192 }
            : request.ReferenceId.HasValue || request.Kind is GateIRelationshipKind.GoverningDecision
                or GateIRelationshipKind.SelectedBudgetVersion or GateIRelationshipKind.SelectedScenario);

    private static string HashRequest(GateIRelationshipMutationCommand request, Guid tenantId, Guid actorId)
    {
        using var stream = new MemoryStream();
        Write(stream, Encoding.UTF8.GetBytes("DITEN-PPM-GATE-I-MUTATION-V1"));
        Write(stream, Encoding.UTF8.GetBytes(tenantId.ToString("D")));
        Write(stream, Encoding.UTF8.GetBytes(actorId.ToString("D")));
        Write(stream, Encoding.UTF8.GetBytes(request.OperationId));
        Write(stream, Encoding.UTF8.GetBytes(request.AggregateId.ToString("D")));
        Write(stream, Encoding.UTF8.GetBytes(request.ExpectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Write(stream, request.CanonicalWrapperUtf8);
        Write(stream, Encoding.UTF8.GetBytes(request.ReferenceId?.ToString("D") ?? "-"));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void Write(Stream stream, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }

    private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static bool IsLowerHex64(string value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string MutationName(GateIRelationshipMutationCommand request) =>
        $"gate-i-{request.Kind.ToString().ToLowerInvariant()}-{request.Action.ToString().ToLowerInvariant()}";
}

public sealed class GateIRelationshipNotFoundException : Exception;
public sealed class GateIRelationshipConflictException(string message) : Exception(message);
public sealed class GateIRelationshipUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
