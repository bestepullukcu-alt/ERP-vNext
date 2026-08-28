using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Binding;

/// <summary>
/// MOD-0167 FU04 — the IN-SERVICE binding proofs: the bound segment (MOD-0167 FU02), the referenced frequency policy
/// (MOD-0165) and the bound content rows (MOD-0162 FU04/FU05) must exist in this tenant and be in a state a play may
/// point at.
/// <para>Everything here is <b>strictly read-only</b>. It calls <c>GetByIdAsync</c> and nothing else: no repository
/// signature is widened, no aggregate is mutated, and <c>ISegmentMembershipReader</c> is deliberately not injected —
/// a template binds a segment's IDENTITY and never sees its members.</para>
/// <para>Display fields (<c>SegmentCodeDisplay</c>, <c>PolicyCodeDisplay</c>, <c>ContentCodeDisplay</c>) and the
/// binding-time version stamps are filled HERE, from the referenced aggregate, so the caller can neither forge them nor
/// make them the source of truth.</para>
/// </summary>
public sealed class StrategyTemplateBindingValidator
{
    private readonly ISegmentRepository _segments;
    private readonly IVisitFrequencyPolicyRepository _policies;
    private readonly IKnowledgePathRepository _paths;
    private readonly IContentEngagementJourneyRepository _journeys;

    public StrategyTemplateBindingValidator(
        ISegmentRepository segments,
        IVisitFrequencyPolicyRepository policies,
        IKnowledgePathRepository paths,
        IContentEngagementJourneyRepository journeys)
    {
        _segments = segments;
        _policies = policies;
        _paths = paths;
        _journeys = journeys;
    }

    /// <summary>
    /// Proves every in-service binding of the template and stamps the derived display/provenance fields.
    /// <para><paramref name="requireActiveSegments"/> is true only on <c>activate</c>: a draft may reference a draft
    /// segment (that is how a play is prepared alongside its population), but putting a play live while its population
    /// is not live would promise the field something that does not exist yet.</para>
    /// </summary>
    public async Task<StrategyTemplateValidation.Failure?> ValidateAsync(
        Guid tenantId,
        TemplateEntity template,
        bool requireActiveSegments,
        CancellationToken cancellationToken)
    {
        var segmentFailure = await ValidateSegmentBindingsAsync(
            tenantId, template, requireActiveSegments, cancellationToken);
        if (segmentFailure is not null)
        {
            return segmentFailure;
        }

        var frequencyFailure = await ValidateFrequencyIntentAsync(tenantId, template, cancellationToken);
        if (frequencyFailure is not null)
        {
            return frequencyFailure;
        }

        return await ValidateContentBindingsAsync(tenantId, template, cancellationToken);
    }

    private async Task<StrategyTemplateValidation.Failure?> ValidateSegmentBindingsAsync(
        Guid tenantId, TemplateEntity template, bool requireActive, CancellationToken cancellationToken)
    {
        foreach (var binding in template.SegmentBindings)
        {
            // Cross-tenant reads come back null, so a foreign segment is "not found" rather than a leak.
            var segment = await _segments.GetByIdAsync(tenantId, binding.SegmentId, cancellationToken);
            if (segment is null)
            {
                return new StrategyTemplateValidation.Failure(
                    $"Segment '{binding.SegmentId}' does not exist in this tenant.",
                    StrategyTemplateErrorCodes.SegmentReferenceNotFound);
            }

            if (segment.IsArchived())
            {
                return new StrategyTemplateValidation.Failure(
                    $"Segment '{segment.SegmentCode}' is archived and cannot be bound.",
                    StrategyTemplateErrorCodes.SegmentArchived);
            }

            if (!string.Equals(segment.SubjectType, template.SubjectType, StringComparison.Ordinal))
            {
                return new StrategyTemplateValidation.Failure(
                    $"Segment '{segment.SegmentCode}' groups '{segment.SubjectType}' but this template targets "
                    + $"'{template.SubjectType}'.",
                    StrategyTemplateErrorCodes.SegmentSubjectTypeMismatch);
            }

            if (requireActive && !segment.IsActive())
            {
                return new StrategyTemplateValidation.Failure(
                    $"Segment '{segment.SegmentCode}' is '{segment.SegmentStatus}'. Every bound segment must be "
                    + "active before the template goes live.",
                    StrategyTemplateErrorCodes.SegmentNotActive, 409);
            }

            // Provenance stamps: read from the segment, never accepted from the caller. They are never refreshed later,
            // so a version drift stays visible instead of being quietly healed.
            binding.SegmentLineageId = segment.VersionLineageId;
            binding.SegmentVersionAtBinding = segment.SegmentVersion;
            binding.SegmentCodeDisplay = segment.SegmentCode;
        }

        return null;
    }

    /// <summary>
    /// Proves a <c>policy-reference</c> intent. Note what this method does NOT do: it never creates or updates a
    /// <see cref="VisitFrequencyPolicy"/>. The repository is used for exactly one call — <c>GetByIdAsync</c>.
    /// </summary>
    private async Task<StrategyTemplateValidation.Failure?> ValidateFrequencyIntentAsync(
        Guid tenantId, TemplateEntity template, CancellationToken cancellationToken)
    {
        var intent = template.FrequencyIntent;
        if (!intent.IsPolicyReference() || intent.VisitFrequencyPolicyId is not { } policyId)
        {
            intent.PolicyCodeDisplay = null;
            return null;
        }

        var policy = await _policies.GetByIdAsync(tenantId, policyId, cancellationToken);
        if (policy is null)
        {
            return new StrategyTemplateValidation.Failure(
                $"Visit frequency policy '{policyId}' does not exist in this tenant.",
                StrategyTemplateErrorCodes.FrequencyPolicyNotFound);
        }

        if (!FrequencyPolicyStatus.IsResolvable(policy.Status))
        {
            return new StrategyTemplateValidation.Failure(
                $"Visit frequency policy '{policy.PolicyCode}' is '{policy.Status}'; only an active policy can be "
                + "referenced.",
                StrategyTemplateErrorCodes.FrequencyPolicyNotActive);
        }

        // A segment-targeted policy that points at a segment this play does not bind would make the template contradict
        // itself. A policy targeting something else (an account, a territory node) is accepted: it is narrower or wider
        // than the play, and /bindings reports that fact rather than hiding it.
        if (string.Equals(policy.TargetType, FrequencyTargetType.Segment, StringComparison.Ordinal)
            && template.SegmentBindings.All(b => b.SegmentId != policy.TargetId))
        {
            return new StrategyTemplateValidation.Failure(
                $"Policy '{policy.PolicyCode}' targets segment '{policy.TargetId}', which this template does not bind.",
                StrategyTemplateErrorCodes.FrequencyPolicyTargetMismatch);
        }

        intent.PolicyCodeDisplay = policy.PolicyCode;
        return null;
    }

    private async Task<StrategyTemplateValidation.Failure?> ValidateContentBindingsAsync(
        Guid tenantId, TemplateEntity template, CancellationToken cancellationToken)
    {
        foreach (var binding in template.ContentBindings)
        {
            if (binding.IsKnowledgePath())
            {
                var path = await _paths.GetByIdAsync(tenantId, binding.ContentRefId, cancellationToken);
                var failure = Check(
                    binding, path is null, path?.IsArchived() ?? false, path?.IsPublished() ?? false,
                    path?.PathCode, path?.PathVersion);
                if (failure is not null)
                {
                    return failure;
                }

                continue;
            }

            var journey = await _journeys.GetByIdAsync(tenantId, binding.ContentRefId, cancellationToken);
            var journeyFailure = Check(
                binding, journey is null, journey?.IsArchived() ?? false, journey?.IsPublished() ?? false,
                journey?.JourneyCode, journey?.JourneyVersion);
            if (journeyFailure is not null)
            {
                return journeyFailure;
            }
        }

        return null;
    }

    /// <summary>The same three questions for both content kinds, so a path and a journey can never drift apart on what
    /// "bindable" means.</summary>
    private static StrategyTemplateValidation.Failure? Check(
        StrategyTemplateContentBinding binding,
        bool missing,
        bool archived,
        bool published,
        string? code,
        string? businessVersion)
    {
        if (missing)
        {
            return new StrategyTemplateValidation.Failure(
                $"Content '{binding.ContentRefType}:{binding.ContentRefId}' does not exist in this tenant.",
                StrategyTemplateErrorCodes.ContentReferenceNotFound);
        }

        if (archived)
        {
            return new StrategyTemplateValidation.Failure(
                $"Content '{code}' is archived and cannot be bound.",
                StrategyTemplateErrorCodes.ContentArchived);
        }

        if (!published)
        {
            return new StrategyTemplateValidation.Failure(
                $"Content '{code}' is not published. A draft story cannot be promised to the field.",
                StrategyTemplateErrorCodes.ContentNotPublished);
        }

        binding.ContentCodeDisplay = code;
        binding.ContentVersionAtBinding = businessVersion;
        return null;
    }
}
