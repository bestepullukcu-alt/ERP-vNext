using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.BenefitRealization;
using Diten.PpmService.Domain.GateI.DecisionTrace;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Infrastructure.GateI;


public static class GateIOwnerReferenceLocalEvidenceTestHost
{
    public static GateIOwnerReferenceLocalEvidencePorts Create(
        HttpClient httpClient,
        IS2SOutboundProofProvider proofProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(proofProvider);
        var client = new GateIOwnerReferenceHttpClients(httpClient, proofProvider);
        return new(client, client, client, client, client);
    }
}

internal sealed class GateIOwnerReferenceHttpClients(
    HttpClient httpClient,
    IS2SOutboundProofProvider proofProvider)
    : IDecisionReferenceValidationPort,
      IBudgetVersionReferenceValidationPort,
      IScenarioPlanningReferenceValidationPort,
      IOutcomeReferenceAuthorityPort,
      IGateIRelationshipAuthority
{
    public async Task<DecisionReferenceProviderResult> ValidateAsync(
        DecisionTraceValidationRequest request,
        DecisionTraceTrustedContext context,
        CancellationToken cancellationToken)
    {
        if (!proofProvider.IsAvailable)
            return new(DecisionReferenceProviderResultKind.Unavailable);
        var rawBody = DecisionBody(request);
        var response = await SendAsync(
            S2SOutboundReceiverProfiles.DecisionRegistry,
            new(context.TenantId, context.EffectiveActorId, context.DelegatedActorId, context.DelegatedActorProofValidated),
            rawBody,
            context.RequestHash,
            cancellationToken).ConfigureAwait(false);
        return response.Status switch
        {
            200 => new(DecisionReferenceProviderResultKind.Resolved, request.Reference.DecisionRevisionReference,
                request.Mode, true, true, DecisionReferenceDisposition.Published),
            401 => new(DecisionReferenceProviderResultKind.AuthenticationFailure),
            403 => new(DecisionReferenceProviderResultKind.PermissionDenied),
            404 => new(DecisionReferenceProviderResultKind.NotFound),
            409 => new(DecisionReferenceProviderResultKind.Conflict),
            _ => new(DecisionReferenceProviderResultKind.Unavailable)
        };
    }

    public async ValueTask<ProducerReferenceValidationResult> ValidateAsync(
        BudgetReferenceValidationRequest request,
        S2SFundingScenarioContextV1 context,
        CancellationToken cancellationToken)
    {
        if (!proofProvider.IsAvailable)
            return new(ProducerReferenceState.Unavailable);
        var rawBody = Encoding.UTF8.GetBytes(request.Wrapper.ToExactJson());
        var response = await SendAsync(
            S2SOutboundReceiverProfiles.Budgeting,
            new(context.TenantId, context.EffectiveActorId, context.DelegatedActorId, context.DelegatedProofValidated),
            rawBody,
            context.RequestHash,
            cancellationToken).ConfigureAwait(false);
        return new(MapProducer(response.Status));
    }

    public async ValueTask<ProducerReferenceValidationResult> ValidateAsync(
        ScenarioReferenceValidationRequest request,
        S2SFundingScenarioContextV1 context,
        CancellationToken cancellationToken)
    {
        if (!proofProvider.IsAvailable)
            return new(ProducerReferenceState.Unavailable);
        var rawBody = Encoding.UTF8.GetBytes(request.Wrapper switch
        {
            InvestmentCaseScenarioVersionReferenceV1 value => value.ToExactJson(),
            InvestmentCaseComparatorOutputReferenceV1 value => value.ToExactJson(),
            SelectedScenarioReferenceV1 value => value.ToExactJson(),
            _ => throw new InvalidOperationException("Unsupported Scenario Planning wrapper.")
        });
        var response = await SendAsync(
            S2SOutboundReceiverProfiles.ScenarioPlanning,
            new(context.TenantId, context.EffectiveActorId, context.DelegatedActorId, context.DelegatedProofValidated),
            rawBody,
            context.RequestHash,
            cancellationToken).ConfigureAwait(false);
        return new(MapProducer(response.Status));
    }

    public async Task<OutcomeReferenceAuthorityResult> ValidateAsync(
        OutcomeReferenceAuthorityRequest request,
        CancellationToken cancellationToken)
    {
        if (!proofProvider.IsAvailable)
            return new(OutcomeReferenceAuthorityDisposition.Unavailable);
        var rawBody = OutcomeBody(request.Reference);
        var response = await SendAsync(
            S2SOutboundReceiverProfiles.OutcomeTracking,
            new(request.TenantId, request.EffectiveActorId, request.DelegatedActorId,
                request.DelegatedActorId is not null),
            rawBody,
            request.RequestHash,
            cancellationToken).ConfigureAwait(false);
        return response.Status switch
        {
            200 => new(OutcomeReferenceAuthorityDisposition.Accepted, request.Reference),
            403 => new(OutcomeReferenceAuthorityDisposition.PermissionDenied),
            404 => new(OutcomeReferenceAuthorityDisposition.MissingOrNonDisclosable),
            409 => new(OutcomeReferenceAuthorityDisposition.IneligibleOrConflicting),
            _ => new(OutcomeReferenceAuthorityDisposition.Unavailable)
        };
    }

    public async Task<GateIAuthorityValidationResult> ValidateAsync(
        GateIAuthorityValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (!proofProvider.IsAvailable)
            return new(503, "ppm_gate_i_provider_not_composed", new string('0', 64), false);
        var receiver = request.Receiver;
        var requestHash = S2SOutboundCanonicalRequestBinding.Compute(
            receiver.Method, receiver.Path, request.CanonicalWrapperUtf8.Span,
            request.TenantId, receiver.Operation, [receiver.Permission]);
        var response = await SendAsync(
            receiver,
            new(
                request.TrustedContext.TenantId,
                request.TrustedContext.EffectiveActorId,
                request.TrustedContext.DelegatedActorId,
                true),
            request.CanonicalWrapperUtf8,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        return response.Status switch
        {
            200 => new(200, "ppm_gate_i_authority_accepted", requestHash, true),
            400 => new(400, "ppm_gate_i_authority_malformed", new string('0', 64), false),
            401 => new(401, "ppm_gate_i_authority_unauthenticated", new string('0', 64), false),
            403 => new(403, "ppm_gate_i_authority_forbidden", new string('0', 64), false),
            404 => new(404, "ppm_gate_i_authority_not_found", new string('0', 64), false),
            409 => new(409, "ppm_gate_i_authority_conflict", new string('0', 64), false),
            _ => new(503, "ppm_gate_i_provider_not_composed", new string('0', 64), false)
        };
    }

    private async Task<(int Status, byte[] Body)> SendAsync(
        S2SOutboundReceiverProfile receiver,
        S2SOutboundTrustedContext trustedContext,
        ReadOnlyMemory<byte> rawBody,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var proof = await proofProvider.IssueAsync(
            new(receiver, trustedContext, rawBody, requestHash),
            cancellationToken).ConfigureAwait(false);
        if (proof.Disposition != S2SOutboundProofDisposition.Issued
            || proof.Proof is not LocalTestS2SOutboundProof localTestProof)
        {
            return (MapProofFailure(proof.Disposition), []);
        }

        using var message = new HttpRequestMessage(new HttpMethod(receiver.Method), receiver.Path)
        {
            Content = new ByteArrayContent(rawBody.ToArray())
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", localTestProof.BearerToken);
        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return ((int)response.StatusCode, body);
    }

    private static int MapProofFailure(S2SOutboundProofDisposition disposition) => disposition switch
    {
        S2SOutboundProofDisposition.Unauthenticated => 401,
        S2SOutboundProofDisposition.Forbidden => 403,
        S2SOutboundProofDisposition.Conflict => 409,
        _ => 503
    };

    private static ProducerReferenceState MapProducer(int status) => status switch
    {
        200 => ProducerReferenceState.Allowed,
        404 => ProducerReferenceState.MissingOrInvisible,
        409 => ProducerReferenceState.IneligibleOrStale,
        _ => ProducerReferenceState.Unavailable
    };

    private static byte[] DecisionBody(DecisionTraceValidationRequest request)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("Mode", request.Mode.ToString());
        writer.WritePropertyName("Reference");
        writer.WriteRawValue(DecisionTraceReferenceCodec.Serialize(request.Reference));
        writer.WriteEndObject();
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] OutcomeBody(OutcomeReferenceV1 reference)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("contractName", reference.ContractName);
        writer.WriteString("contractVersion", reference.ContractVersion);
        writer.WriteString("outcomeId", reference.OutcomeId.ToString("D"));
        writer.WriteString("outcomeVersionId", reference.OutcomeVersionId.ToString("D"));
        writer.WriteNumber("outcomeVersionNumber", reference.OutcomeVersionNumber);
        writer.WriteEndObject();
        writer.Flush();
        return stream.ToArray();
    }
}
