using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.BusinessReferenceData;
using Diten.Platform.Common.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// PSS-012 Business Reference Data Stewardship API. Thin controller: every action delegates to MediatR
/// and returns <see cref="CustomBaseController.CreateActionResultInstance{T}"/>. Authorization is enforced
/// per action via the central <see cref="HasPermissionAttribute"/>; correlation comes from
/// <see cref="ICorrelationContext"/>; business error-to-HTTP mapping lives in the Application-layer
/// BusinessReferenceDataExceptionBehavior — never in this controller.
/// </summary>
[ApiController]
[Route("api/v1/reference-data")]
[Authorize]
public sealed class BusinessReferenceDataController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;
    private readonly ICurrentUserContext _currentUser;

    public BusinessReferenceDataController(
        IMediator mediator,
        ICorrelationContext correlationContext,
        ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
        _currentUser = currentUser;
    }

    [HttpGet("sets")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetSets(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "scope_type")] string? scopeType,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 20,
        [FromQuery(Name = "sort")] string sort = "-createdAt",
        CancellationToken ct = default,
        [FromQuery(Name = "pageNumber")] int? pageNumber = null,
        [FromQuery(Name = "pageSize")] int? pageSizeAlias = null,
        [FromQuery(Name = "limit")] int? limit = null,
        [FromQuery(Name = "offset")] int? offset = null)
    {
        var effectivePageSize = pageSizeAlias ?? limit ?? pageSize;
        var effectivePage = pageNumber ?? page;
        if (limit is > 0 && offset is >= 0)
        {
            effectivePage = (offset.Value / limit.Value) + 1;
        }

        var response = await _mediator.Send(
            new GetBusinessReferenceDataSetsQuery(search, status, scopeType, effectivePage, effectivePageSize, sort), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("scope-types")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetScopeTypes(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataScopeTypesQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("sets")]
    [HasPermission("Platform.BusinessReferenceData.Create")]
    public async Task<IActionResult> CreateSet([FromBody] CreateBusinessReferenceDataSetRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new CreateBusinessReferenceDataSetCommand(request.SetCode, request.Name, request.ScopeType, request.Description, request.Status, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("sets/{setId:guid}")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetSet(Guid setId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataSetByIdQuery(setId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("sets/{setId:guid}/versions")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetSetVersions(Guid setId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataSetVersionsQuery(setId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPatch("sets/{setId:guid}")]
    [HasPermission("Platform.BusinessReferenceData.Update")]
    public async Task<IActionResult> PatchSet(Guid setId, [FromBody] PatchBusinessReferenceDataSetRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new PatchBusinessReferenceDataSetCommand(
                setId,
                request.RowVersion,
                request.Name,
                request.Description,
                request.Status,
                request.SetCode,
                request.ScopeType,
                CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("sets/{setId:guid}/versions")]
    [HasPermission("Platform.BusinessReferenceData.Version.Create")]
    public async Task<IActionResult> CreateVersion(Guid setId, [FromBody] CreateBusinessReferenceDataVersionRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new CreateBusinessReferenceDataVersionCommand(setId, request?.SourceVersionId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("versions/{versionId:guid}")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetVersion(Guid versionId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataVersionByIdQuery(versionId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("versions/{versionId:guid}/values")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetVersionValues(Guid versionId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataVersionValuesQuery(versionId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("versions/{versionId:guid}/attribute-definitions")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetVersionAttributeDefinitions(Guid versionId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataVersionAttributeDefinitionsQuery(versionId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("versions/{versionId:guid}/values")]
    [HasPermission("Platform.BusinessReferenceData.Version.Update")]
    public async Task<IActionResult> ReplaceVersionValues(
        Guid versionId,
        [FromBody] ReplaceBusinessReferenceDataVersionValuesRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new ReplaceBusinessReferenceDataVersionValuesCommand(
                versionId,
                ResolveActorId(),
                CorrelationId,
                ResolveExpectedConcurrencyToken(request.ExpectedConcurrencyToken),
                request.Values.Select(x => new BusinessReferenceDataVersionValueInputModel(
                    x.Code,
                    x.Label,
                    x.Description,
                    x.IsActive,
                    x.SortOrder,
                    x.ParentValueCode,
                    x.Attributes)).ToList()), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("versions/{versionId:guid}/attribute-definitions")]
    [HasPermission("Platform.BusinessReferenceData.Version.Update")]
    public async Task<IActionResult> ReplaceVersionAttributeDefinitions(
        Guid versionId,
        [FromBody] ReplaceBusinessReferenceDataVersionAttributeDefinitionsRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new ReplaceBusinessReferenceDataVersionAttributeDefinitionsCommand(
                versionId,
                ResolveActorId(),
                CorrelationId,
                ResolveExpectedConcurrencyToken(request.ExpectedConcurrencyToken),
                request.Definitions.Select(x => new BusinessReferenceDataAttributeDefinitionInputModel(
                    x.AttributeCode,
                    x.DisplayName,
                    x.DataType,
                    x.IsRequired)).ToList()), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("versions/{versionId:guid}/mappings")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetVersionMappings(Guid versionId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataVersionMappingsQuery(versionId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("versions/{versionId:guid}/mappings")]
    [HasPermission("Platform.BusinessReferenceData.Version.Update")]
    public async Task<IActionResult> ReplaceVersionMappings(
        Guid versionId,
        [FromBody] ReplaceBusinessReferenceDataVersionMappingsRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new ReplaceBusinessReferenceDataVersionMappingsCommand(
                versionId,
                ResolveActorId(),
                CorrelationId,
                ResolveExpectedConcurrencyToken(request.ExpectedConcurrencyToken),
                request.Mappings.Select(x => new BusinessReferenceDataMappingInputModel(
                    x.MappingKey,
                    x.SourceValueCode,
                    x.TargetCode,
                    x.TargetLabel)).ToList()), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("versions/{versionId:guid}/validate")]
    [HasPermission("Platform.BusinessReferenceData.Version.Validate")]
    public async Task<IActionResult> ValidateVersion(Guid versionId, CancellationToken ct)
    {
        var response = await _mediator.Send(new ValidateBusinessReferenceDataVersionCommand(versionId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("versions/{versionId:guid}/submit")]
    [HasPermission("Platform.BusinessReferenceData.Version.Submit")]
    public async Task<IActionResult> SubmitVersion(
        Guid versionId,
        [FromBody] SubmitBusinessReferenceDataVersionRequest? request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new SubmitBusinessReferenceDataVersionCommand(
                versionId,
                ResolveActorId(),
                CorrelationId,
                ResolveExpectedConcurrencyToken(request?.ExpectedConcurrencyToken),
                ToEvidenceInput(request),
                request?.OverrideAction ?? false,
                request?.OverrideReason), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("versions/{versionId:guid}/approve")]
    [HasPermission("Platform.BusinessReferenceData.Version.Approve")]
    public async Task<IActionResult> ApproveVersion(
        Guid versionId,
        [FromBody] ApproveBusinessReferenceDataVersionRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new ApproveBusinessReferenceDataVersionCommand(
                versionId,
                ResolveActorId(),
                CorrelationId,
                ResolveExpectedConcurrencyToken(request.ExpectedConcurrencyToken),
                ResolveApprovalAction(request.Decision),
                request.RejectionReason,
                request.OverrideAction,
                request.OverrideReason,
                ToEvidenceInput(request),
                request.Comment,
                request.TargetStep,
                ResolveIdempotencyKey()), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("versions/{versionId:guid}/publish")]
    [HasPermission("Platform.BusinessReferenceData.Version.Publish")]
    public async Task<IActionResult> PublishVersion(
        Guid versionId,
        [FromBody] PublishBusinessReferenceDataVersionRequest request,
        CancellationToken ct)
    {
        if (request.OverrideAction)
        {
            return CreateActionResultInstance(Response<BusinessReferenceDataVersionDetailModel>.Fail("publish_override_permission_required", 403));
        }

        var idempotencyKey = ResolveIdempotencyKey();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return CreateActionResultInstance(Response<BusinessReferenceDataVersionDetailModel>.Fail("idempotency_key_required", 400));
        }

        var response = await _mediator.Send(
            new PublishBusinessReferenceDataVersionCommand(
                versionId,
                ResolveActorId(),
                CorrelationId,
                idempotencyKey,
                request.PublishMode,
                request.PublishAt,
                ResolveExpectedConcurrencyToken(request.ExpectedConcurrencyToken),
                OverrideAction: false,
                request.OverrideReason), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("versions/{versionId:guid}/publish-override")]
    [HasPermission("Platform.BusinessReferenceData.Version.PublishOverride")]
    public async Task<IActionResult> PublishVersionOverride(
        Guid versionId,
        [FromBody] PublishBusinessReferenceDataVersionRequest request,
        CancellationToken ct)
    {
        var idempotencyKey = ResolveIdempotencyKey();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return CreateActionResultInstance(Response<BusinessReferenceDataVersionDetailModel>.Fail("idempotency_key_required", 400));
        }

        var response = await _mediator.Send(
            new PublishBusinessReferenceDataVersionCommand(
                versionId,
                ResolveActorId(),
                CorrelationId,
                idempotencyKey,
                request.PublishMode,
                request.PublishAt,
                ResolveExpectedConcurrencyToken(request.ExpectedConcurrencyToken),
                OverrideAction: true,
                request.OverrideReason), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("sets/{setCode}/published-values")]
    [HasPermission("Platform.BusinessReferenceData.Consumer.Read")]
    public async Task<IActionResult> GetPublishedValues(
        string setCode,
        [FromQuery(Name = "scope_key")] string? scopeKey,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataPublishedValuesQuery(setCode, scopeKey), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("sets/{setCode}/values")]
    [HasPermission("Platform.BusinessReferenceData.Consumer.Read")]
    public async Task<IActionResult> GetConsumerValues(
        string setCode,
        [FromQuery(Name = "scope_key")] string? scopeKey,
        [FromQuery(Name = "version")] int? version,
        [FromQuery(Name = "as_of_date")] DateTimeOffset? asOfDate,
        [FromQuery(Name = "include_deprecated")] bool includeDeprecated = false,
        [FromQuery(Name = "include_attributes")] bool includeAttributes = false,
        [FromQuery(Name = "include_mappings")] bool includeMappings = false,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(
            new GetBusinessReferenceDataValuesQuery(
                setCode, scopeKey, version, asOfDate, includeDeprecated, includeAttributes, includeMappings), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("sets/{setCode}/hierarchy")]
    [HasPermission("Platform.BusinessReferenceData.Consumer.Read")]
    public async Task<IActionResult> GetConsumerHierarchy(
        string setCode,
        [FromQuery(Name = "scope_key")] string? scopeKey,
        [FromQuery(Name = "version")] int? version,
        [FromQuery(Name = "as_of_date")] DateTimeOffset? asOfDate,
        [FromQuery(Name = "include_deprecated")] bool includeDeprecated = false,
        [FromQuery(Name = "include_attributes")] bool includeAttributes = false,
        [FromQuery(Name = "include_mappings")] bool includeMappings = false,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(
            new GetBusinessReferenceDataHierarchyQuery(
                setCode, scopeKey, version, asOfDate, includeDeprecated, includeAttributes, includeMappings), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("usage-registrations")]
    [HasPermission("Platform.BusinessReferenceData.Usage.Register")]
    public async Task<IActionResult> RegisterUsage([FromBody] RegisterBusinessReferenceDataUsageRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new RegisterBusinessReferenceDataUsageCommand(
                request.SetCode,
                request.ConsumerModule,
                request.ConsumerName,
                request.ConsumerEndpoint,
                request.ScopeType,
                request.ScopeKey,
                request.VersionPin,
                request.AsOfDate,
                request.ResolutionMode,
                request.Criticality,
                request.Notes,
                ResolveActorId(),
                CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("usage-registrations")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetUsageRegistrations([FromQuery(Name = "set_code")] string setCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataUsageRegistrationsQuery(setCode), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("usage-registrations/form-options")]
    [HasPermission("Platform.BusinessReferenceData.Read")]
    public async Task<IActionResult> GetUsageFormOptions([FromQuery(Name = "set_code")] string setCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBusinessReferenceDataUsageFormOptionsQuery(setCode), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("usage-registrations/bulk")]
    [HasPermission("Platform.BusinessReferenceData.Usage.Register")]
    public async Task<IActionResult> BulkDeleteUsageRegistrations([FromBody] List<Guid> ids, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new DeactivateBusinessReferenceDataUsageRegistrationsBulkCommand(ids ?? [], ResolveActorId(), CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("usage-registrations/{usageRegistrationId:guid}")]
    [HasPermission("Platform.BusinessReferenceData.Usage.Register")]
    public async Task<IActionResult> DeleteUsageRegistration(Guid usageRegistrationId, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new DeactivateBusinessReferenceDataUsageRegistrationCommand(usageRegistrationId, ResolveActorId(), CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("imports/preview")]
    [HasPermission("Platform.BusinessReferenceData.Import.Preview")]
    public async Task<IActionResult> PreviewImport([FromBody] PreviewBusinessReferenceDataImportRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new PreviewBusinessReferenceDataImportCommand(
                request.TargetDraftVersionId,
                request.FileName,
                request.Format,
                request.ContentBase64,
                ResolveActorId(),
                CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("imports/{previewId:guid}/commit")]
    [HasPermission("Platform.BusinessReferenceData.Import.Commit")]
    public async Task<IActionResult> CommitImport(Guid previewId, CancellationToken ct)
    {
        var idempotencyKey = ResolveIdempotencyKey();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return CreateActionResultInstance(Response<BusinessReferenceDataImportCommitResultModel>.Fail("idempotency_key_required", 400));
        }

        var response = await _mediator.Send(
            new CommitBusinessReferenceDataImportCommand(previewId, idempotencyKey, ResolveActorId(), CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("fixtures/evidence-required:provision")]
    [HasPermission("Platform.BusinessReferenceData.Fixture.Manage")]
    public async Task<IActionResult> ProvisionEvidenceRequiredFixture(
        [FromBody] ProvisionBusinessReferenceDataEvidenceFixtureRequest request,
        CancellationToken ct)
    {
        if (!IsFixtureContractEnabled())
        {
            return CreateActionResultInstance(Response<BusinessReferenceDataEvidenceFixtureProvisionModel>.Fail("fixture_contract_disabled", 404));
        }

        if (request is null || !request.ConfirmFixtureOwned)
        {
            return CreateActionResultInstance(Response<BusinessReferenceDataEvidenceFixtureProvisionModel>.Fail("fixture_confirmation_required", 400));
        }

        var response = await _mediator.Send(
            new ProvisionBusinessReferenceDataEvidenceFixtureCommand(
                request.FixtureCode,
                request.SetCode,
                request.SetName,
                request.RequirementCode,
                request.ValueCode,
                request.ValueLabel,
                ResolveActorId(),
                CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("fixtures/evidence-required/{setId:guid}:retire")]
    [HasPermission("Platform.BusinessReferenceData.Fixture.Manage")]
    public async Task<IActionResult> RetireEvidenceRequiredFixture(
        Guid setId,
        [FromBody] RetireBusinessReferenceDataEvidenceFixtureSetRequest request,
        CancellationToken ct)
    {
        if (!IsFixtureContractEnabled())
        {
            return CreateActionResultInstance(Response<BusinessReferenceDataEvidenceFixtureRetireModel>.Fail("fixture_contract_disabled", 404));
        }

        if (request is null || !request.ConfirmFixtureOwned)
        {
            return CreateActionResultInstance(Response<BusinessReferenceDataEvidenceFixtureRetireModel>.Fail("fixture_confirmation_required", 400));
        }

        var response = await _mediator.Send(
            new RetireBusinessReferenceDataEvidenceFixtureSetCommand(
                request.FixtureCode,
                setId,
                request.ExpectedRowVersion,
                ResolveActorId(),
                CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // --- Request-context helpers (HTTP concerns only; no business logic) ---

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : _correlationContext.CorrelationId!;

    private string ResolveActorId() =>
        _currentUser.IsAuthenticated ? _currentUser.UserId.ToString("N") : "system";

    private string? ResolveExpectedConcurrencyToken(string? fromBody)
    {
        if (!string.IsNullOrWhiteSpace(fromBody))
        {
            return fromBody.Trim().Trim('"');
        }

        if (!Request.Headers.TryGetValue("If-Match", out var ifMatch))
        {
            return null;
        }

        var raw = ifMatch.ToString().Trim();
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim('"');
    }

    private string? ResolveIdempotencyKey()
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idem))
        {
            return null;
        }

        var raw = idem.ToString().Trim();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static bool IsFixtureContractEnabled()
    {
        var explicitFlag = Environment.GetEnvironmentVariable("BusinessReferenceData_ENABLE_FIXTURE_CONTRACT");
        if (string.Equals(explicitFlag, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase)
               || string.Equals(env, "Local", StringComparison.OrdinalIgnoreCase)
               || string.Equals(env, "Test", StringComparison.OrdinalIgnoreCase);
    }

    private static BusinessReferenceDataEvidenceInput ToEvidenceInput(SubmitBusinessReferenceDataVersionRequest? request)
        => request is null
            ? BusinessReferenceDataEvidenceInput.FromLegacy(null)
            : new BusinessReferenceDataEvidenceInput(
                request.EvidenceRef,
                request.EvidenceLinkId,
                request.DocumentVersionId,
                request.RequirementCode);

    private static BusinessReferenceDataEvidenceInput ToEvidenceInput(ApproveBusinessReferenceDataVersionRequest request)
        => new(
            request.EvidenceRef,
            request.EvidenceLinkId,
            request.DocumentVersionId,
            request.RequirementCode);

    private static BusinessReferenceDataWorkflowTransitionAction ResolveApprovalAction(string? decision)
    {
        var normalized = string.IsNullOrWhiteSpace(decision)
            ? "approve"
            : decision.Trim().Replace("-", "_", StringComparison.OrdinalIgnoreCase);

        return normalized.ToLowerInvariant() switch
        {
            "reject" => BusinessReferenceDataWorkflowTransitionAction.Reject,
            "request_info" or "requestinfo" => BusinessReferenceDataWorkflowTransitionAction.RequestInfo,
            _ => BusinessReferenceDataWorkflowTransitionAction.Approve
        };
    }
}
