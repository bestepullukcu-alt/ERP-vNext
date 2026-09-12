using System.Reflection;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 — guards for the decisions that are easy to undo by accident later.
/// <para>
/// These are mostly <b>negative-capability</b> tests: they assert that something is NOT present. That is
/// unusual and deliberate. AD-6 says physical destruction does not exist yet; AD-4 says no binary payload
/// travels in a JSON body; AD-5 says the store authorises for itself. Each is a property that a future edit
/// could silently remove, and nothing else in the suite would notice.
/// </para>
/// </summary>
public sealed class DocumentRepositoryContractGuardTests
{
    private static readonly Assembly ApplicationAssembly = typeof(DocumentRepositoryService).Assembly;

    // ── AD-4: no byte[] anywhere on the repository contract ──────────────────

    [Fact]
    public void Store_request_carries_a_stream_and_no_byte_array()
    {
        var content = typeof(ContentStoreRequest).GetProperty(nameof(ContentStoreRequest.Content));

        Assert.NotNull(content);
        Assert.Equal(typeof(Stream), content!.PropertyType);

        Assert.DoesNotContain(
            typeof(ContentStoreRequest).GetProperties(),
            p => p.PropertyType == typeof(byte[]));
    }

    [Fact]
    public void No_repository_contract_type_exposes_a_binary_payload_member()
    {
        Type[] contractTypes =
        [
            typeof(ContentStoreRequest), typeof(ContentStoreResult), typeof(ContentStreamResult),
            typeof(RepositoryObjectModel), typeof(StoreRepositoryObjectInput), typeof(RepositoryObjectContentHandle)
        ];

        foreach (var type in contractTypes)
        {
            Assert.DoesNotContain(type.GetProperties(), p => p.PropertyType == typeof(byte[]));
            // A base64 string member would reintroduce the same problem in a different coat.
            Assert.DoesNotContain(type.GetProperties(),
                p => p.Name.Contains("Base64", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ── ObjectKey never leaves the service ───────────────────────────────────

    [Fact]
    public void Client_facing_model_has_no_object_key_member()
    {
        Assert.DoesNotContain(typeof(RepositoryObjectModel).GetProperties(),
            p => p.Name.Contains("ObjectKey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Store_input_does_not_accept_an_object_key_from_the_caller()
    {
        Assert.DoesNotContain(typeof(StoreRepositoryObjectInput).GetProperties(),
            p => p.Name.Contains("ObjectKey", StringComparison.OrdinalIgnoreCase));
    }

    // ── AD-6: the purge path does not exist ──────────────────────────────────

    [Fact]
    public void The_storage_seam_exposes_no_purge_or_destroy_operation()
    {
        var operations = typeof(IContentStorageGateway).GetMethods().Select(m => m.Name).ToArray();

        Assert.Contains("StoreAsync", operations);
        Assert.Contains("OpenReadAsync", operations);
        Assert.Contains("TryDeleteAsync", operations);
        Assert.DoesNotContain(operations, n =>
            n.Contains("Purge", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Destroy", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Dispose", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_repository_service_exposes_no_purge_destroy_or_cascade_operation()
    {
        var operations = typeof(DocumentRepositoryService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

        Assert.DoesNotContain(operations, n =>
            n.Contains("Purge", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Destroy", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Cascade", StringComparison.OrdinalIgnoreCase));

        // The only removal operation is compensation, and it is named so it cannot be mistaken for deletion.
        Assert.Contains("CompensateAsync", operations);
    }

    // The whole feature must not contain a scheduler/background sweep: destruction is FU05's, and a sweep
    // here would be the natural place for someone to bolt one on.
    [Fact]
    public void The_document_repository_feature_registers_no_scheduled_or_background_job()
    {
        var featureTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith(
                "Diten.Platform.Application.Features.DocumentRepository", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.NotEmpty(featureTypes);
        Assert.DoesNotContain(featureTypes, t =>
            t.Name.Contains("Job", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Sweep", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Scheduler", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Recurring", StringComparison.OrdinalIgnoreCase));
    }

    // AD-7: purge.execute is segregated and must NOT be defined by FU01 — defining it would let an
    // implementer wire a destruction path before MOD-0030 owns the retention decision.
    [Fact]
    public void FU01_defines_only_read_and_manage_and_never_a_purge_permission()
    {
        var keys = typeof(DocumentRepositoryPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        Assert.Equal(
            ["platform.document-repository.manage", "platform.document-repository.read"],
            keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());

        Assert.DoesNotContain(keys, k => k.Contains("purge", StringComparison.OrdinalIgnoreCase));
    }

    // ── OD-3: namespace is locked ────────────────────────────────────────────

    [Fact]
    public void Permission_keys_use_the_locked_document_repository_namespace_and_pks001_shape()
    {
        var keys = typeof(DocumentRepositoryPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        foreach (var key in keys)
        {
            // PKS-001 §1: lowercase-dotted, at least three segments, hyphen as the in-segment separator.
            Assert.Equal(key.ToLowerInvariant(), key);
            Assert.True(key.Split('.').Length >= 3, $"'{key}' must have at least 3 segments.");
            Assert.DoesNotContain('_', key);

            // PKS-001 §4: one owning module per namespace — MOD-0029's namespace is NOT extended.
            Assert.StartsWith("platform.document-repository.", key, StringComparison.Ordinal);
            Assert.DoesNotContain("document-management", key, StringComparison.Ordinal);
        }
    }

    // ── AD-5: every entry point authorises for itself ────────────────────────

    [Fact]
    public void Controller_requires_authentication_and_every_action_declares_a_permission()
    {
        var controller = typeof(DocumentRepositoryController);

        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());

        var actions = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToArray();

        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            var permission = action.GetCustomAttribute<HasPermissionAttribute>();
            Assert.True(permission is not null, $"{action.Name} has no [HasPermission] — AD-5 requires per-action authorisation.");
        }
    }

    [Fact]
    public void Controller_exposes_no_purge_or_delete_endpoint()
    {
        var actions = typeof(DocumentRepositoryController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToArray();

        Assert.DoesNotContain(actions, n =>
            n.Contains("Purge", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Destroy", StringComparison.OrdinalIgnoreCase) ||
            n.Equals("Delete", StringComparison.OrdinalIgnoreCase));
    }
}
