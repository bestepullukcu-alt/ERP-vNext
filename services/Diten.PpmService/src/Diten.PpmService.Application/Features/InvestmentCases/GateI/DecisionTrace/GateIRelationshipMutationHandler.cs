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


public sealed class GateIRelationshipMutationHandler(
    IGateIRelationshipAuthority authority,
    IGateIRelationshipMutationPersistence persistence,
    IPpmAccessAuthorizer access,
    ITenantContext tenant,
    ICurrentActorContext actor,
    ICorrelationContext correlation,
    IGateITrustedMutationContextAccessor trustedContextAccessor)
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

        var trustedContext = trustedContextAccessor.Current;
        if (trustedContext is null)
            return Response<GateIRelationshipMutationResult>.Fail("S2S trusted context is invalid.", 401);

        if (trustedContext.TenantId != tenant.TenantId
            || trustedContext.EffectiveActorId != actor.ActorId
            || !string.Equals(trustedContext.OperationId, request.OperationId, StringComparison.Ordinal))
        {
            return Response<GateIRelationshipMutationResult>.Fail("S2S trusted context is invalid.", 401);
        }
        if (trustedContext.Permissions.Count != 1
            || !string.Equals(trustedContext.Permissions[0], permission, StringComparison.Ordinal))
            return Response<GateIRelationshipMutationResult>.Fail("S2S trusted permission is forbidden.", 403);

        var requestHash = HashRequest(request, trustedContext);
        var preliminary = new GateIMutationScope(
            tenant.TenantId, actor.ActorId, correlation.CorrelationId,
            request.OperationId, request.IdempotencyKey, requestHash, string.Empty);
        var receipt = await persistence.ReconcileAsync(preliminary, cancellationToken);
        if (receipt.Disposition == GateIReceiptDisposition.Conflict)
            return Response<GateIRelationshipMutationResult>.Fail("Idempotency key payload conflict.", 409);

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
                    request.Kind, request.Action, trustedContext,
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
        if (receipt.Disposition == GateIReceiptDisposition.Matching)
        {
            var provenanceReceipt = await persistence.ReconcileAsync(scope, cancellationToken);
            if (provenanceReceipt.Disposition == GateIReceiptDisposition.Conflict)
                return Response<GateIRelationshipMutationResult>.Fail("Idempotency key provenance conflict.", 409);
            if (provenanceReceipt is { Disposition: GateIReceiptDisposition.Matching, StoredResult: not null })
                return Response<GateIRelationshipMutationResult>.Success(
                    provenanceReceipt.StoredResult with { Replayed = true });
            return Response<GateIRelationshipMutationResult>.Fail(
                "Idempotency receipt provenance is indeterminate.", 503);
        }

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

    private static string HashRequest(
        GateIRelationshipMutationCommand request,
        GateITrustedMutationContext trustedContext)
    {
        using var stream = new MemoryStream();
        Write(stream, Encoding.UTF8.GetBytes("DITEN-PPM-GATE-I-MUTATION-V1"));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.TenantId.ToString("D")));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.EffectiveActorId.ToString("D")));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.DelegatedActorId.ToString("D")));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.DelegationId.ToString("D")));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.ServicePrincipalId.ToString("D")));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.CredentialId.ToString("D")));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.ClientId));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.Issuer));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.Audience));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.TokenType));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.ProtocolScope));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.OperationId));
        foreach (var permission in trustedContext.Permissions)
            Write(stream, Encoding.UTF8.GetBytes(permission));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.RequestHash));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.TenantGrantVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.ServicePrincipalVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Write(stream, Encoding.UTF8.GetBytes(trustedContext.CredentialGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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
