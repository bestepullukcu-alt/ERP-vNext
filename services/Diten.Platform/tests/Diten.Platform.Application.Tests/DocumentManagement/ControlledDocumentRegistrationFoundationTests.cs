using Diten.Platform.API.Controllers;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Validators;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class ControlledDocumentRegistrationFoundationTests
{
    [Fact]
    public void Operation_tracks_durable_progress_without_delete_state()
    {
        var operation = NewOperation();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var registerId = Guid.NewGuid();

        operation.MarkContentStored("provider:key", new string('a', 64), "{}", "test");
        operation.MarkDocumentCreated(documentId, versionId, "test");
        operation.MarkRegisterCreated(registerId, "test");
        operation.MarkLinked("test");
        operation.MarkCompleted("test");

        Assert.Equal(ControlledDocumentRegistrationStatus.Completed, operation.Status);
        Assert.Equal(documentId, operation.ControlledDocumentId);
        Assert.Equal(versionId, operation.ControlledDocumentVersionId);
        Assert.Equal(registerId, operation.MasterRegisterEntryId);
        Assert.DoesNotContain(Enum.GetNames<ControlledDocumentRegistrationStatus>(),
            x => x.Contains("delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Api_request_exposes_no_server_owned_registration_fields()
    {
        var names = typeof(CreateControlledDocumentRegistrationApiRequest).GetProperties()
            .Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var forbidden in new[]
        {
            "TenantId", "EffectiveDate", "LifecycleStatus", "RegisterStatus", "PermanentUid",
            "DocumentCode", "ApprovalStatus", "ReleaseGateStatus", "SignatureStatus"
        })
        {
            Assert.DoesNotContain(forbidden, names);
        }
    }

    [Fact]
    public void Create_validator_rejects_template_and_missing_core_shape_at_service_boundary()
    {
        var validator = new CreateControlledDocumentRegistrationValidator();
        var command = new CreateControlledDocumentRegistrationCommand(
            new("", "", "", "", "", null, null, "", null, Guid.Empty, null, null, 0, null,
                Guid.Empty, Guid.Empty, new("", null, "")), "corr");

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("IdempotencyKey", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("DocumentTitle", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("InitialFile.FileName", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_validator_requires_parent_for_variant()
    {
        var validator = new CreateControlledDocumentRegistrationValidator();
        var input = new CreateControlledDocumentRegistrationInput(
            "idem-1", "Variant Title", "Sop", "Major", "Sop", null, null, "en", null,
            Guid.NewGuid(), null, null, 12, "10y", Guid.NewGuid(), Guid.NewGuid(), new("f.pdf", null, "QQ=="))
        {
            Kind = Diten.Platform.Domain.Enums.DocumentManagement.RegistrationKind.Variant,
            AuthorUserId = Guid.NewGuid(),
            GoverningLanguageId = "en",
            RetentionClassId = "10y"
        };

        var result = validator.Validate(new CreateControlledDocumentRegistrationCommand(input, "corr"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("ParentRegisterEntryId", StringComparison.Ordinal));
    }

    [Fact]
    public void Controller_has_exact_routes_and_no_delete_endpoint()
    {
        var methods = typeof(DocumentManagementControlledDocumentRegistrationController).GetMethods()
            .Where(x => x.DeclaringType == typeof(DocumentManagementControlledDocumentRegistrationController))
            .ToList();

        Assert.Contains(methods, x => Route<HttpPostAttribute>(x) == "controlled-document-registrations");
        Assert.Contains(methods, x => Route<HttpGetAttribute>(x) == "controlled-document-registrations/{operationId:guid}");
        Assert.Contains(methods, x => Route<HttpPostAttribute>(x) == "controlled-document-registrations/{operationId:guid}/retry");
        Assert.Contains(methods, x => Route<HttpGetAttribute>(x) == "controlled-documents/{controlledDocumentId:guid}/master-register");
        Assert.DoesNotContain(methods, x => x.GetCustomAttributes(typeof(HttpDeleteAttribute), true).Length > 0);
    }

    [Fact]
    public void Create_endpoint_requires_registration_and_all_downstream_permissions()
    {
        var create = typeof(DocumentManagementControlledDocumentRegistrationController).GetMethod("Create")!;
        var permissions = create.GetCustomAttributes(typeof(HasPermissionAttribute), true)
            .Cast<HasPermissionAttribute>().Select(x => x.Permission).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(ControlledDocumentRegistrationPermissions.Create, permissions);
        Assert.Contains("platform.document-management.master-register.manage", permissions);
        Assert.Contains("platform.document-management.master-register.link", permissions);
        Assert.Contains(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate, permissions);
    }

    [Fact]
    public void Failure_detail_is_single_line_and_bounded()
    {
        var operation = NewOperation();
        var longDetail = string.Concat(Enumerable.Repeat("secret\r\n", 300));
        var sanitized = longDetail.Replace('\r', ' ').Replace('\n', ' ').Trim()[..1000];

        operation.MarkFailure("FAILED", sanitized, true, "test");

        Assert.NotNull(operation.FailureDetail);
        Assert.True(operation.FailureDetail!.Length <= 1000);
        Assert.DoesNotContain('\r', operation.FailureDetail);
        Assert.DoesNotContain('\n', operation.FailureDetail);
    }

    private static ControlledDocumentRegistrationOperation NewOperation() => new()
    {
        TenantId = Guid.NewGuid(),
        IdempotencyKey = "idem",
        CorrelationId = "corr",
        CreatedBy = "test"
    };

    private static string? Route<T>(System.Reflection.MethodInfo method) where T : HttpMethodAttribute =>
        method.GetCustomAttributes(typeof(T), true).Cast<T>().SingleOrDefault()?.Template;
}
