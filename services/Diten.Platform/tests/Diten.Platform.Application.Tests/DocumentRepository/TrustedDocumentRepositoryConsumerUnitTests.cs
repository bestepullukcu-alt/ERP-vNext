using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Features.DocumentRepository.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 DL-2 — the unit-level proofs (§17 "Unit"): the executor's claim defence branch (the ONLY proof of a
/// branch the real scheme can never reach on a host — AC-16), the counting stream (AC-35 (e)), the store handler's
/// classification order, and r8 D-1: which token the irreversible steps get on each of the three paths.
/// </summary>
public sealed class TrustedDocumentRepositoryConsumerUnitTests
{
    private static readonly Guid Client = TrustedDocumentRepositoryConsumerGateTests.Client;
    private static readonly Guid Tenant = TrustedDocumentRepositoryConsumerGateTests.TenantA;

    // ── AC-16 — the claim defence branch.

    [Fact]
    public async Task Claim_defence_an_authenticated_principal_without_tenant_id_is_403_with_one_unauthenticated_record_and_no_command()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, Client.ToString("D")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new Claim("actor_type", "service"),
            new Claim("service_name", "Diten.MDM"),
            new Claim(JwtRegisteredClaimNames.Aud, TrustedServiceTokenValidationExtensions.DocumentRepositoryRequiredAudience)
        ], "hand-built"));
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), TrustedServiceTokenValidationExtensions.DocumentRepositoryAuthenticationScheme))
            .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(principal, "hand-built")));
        var services = new ServiceCollection().AddSingleton(authentication.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Headers.Authorization = "Bearer header.payload.signature";
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var audit = new RecordingAuditService();
        var objects = new Mock<IRepositoryObjectRepository>(MockBehavior.Strict);
        var executor = new TrustedDocumentRepositoryConsumerRequestExecutor(
            sender.Object,
            new TenantContext(),
            objects.Object,
            new TrustedDocumentRepositoryRefusalAudit(audit, new TrustedDocumentRepositoryRefusalAudit.UnauthenticatedLimiter(),
                NullLogger<TrustedDocumentRepositoryRefusalAudit>.Instance),
            Options.Create(new TrustedDocumentRepositoryConsumerAuthorizationOptions
            {
                Entries = [new() { ClientId = Client, ServiceName = "Diten.MDM",
                    Audience = TrustedServiceTokenValidationExtensions.DocumentRepositoryRequiredAudience,
                    ConsumerModule = "MOD-0217", Scopes = ["Documents"] }]
            }),
            NullLogger<TrustedDocumentRepositoryConsumerRequestExecutor>.Instance);

        var result = await executor.GetAsync(http, Guid.NewGuid());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerForbidden, Assert.IsType<Response<object>>(objectResult.Value).ReasonCode);
        sender.VerifyNoOtherCalls();
        objects.VerifyNoOtherCalls();
        var record = Assert.Single(audit.Requests);
        Assert.True(record.IsPlatformGlobal);
        Assert.Equal(AuditActorType.System, record.ActorType);
        Assert.Equal(AuditOutcome.Denied, record.Outcome);
    }

    // ── AC-35 (e) — the counting stream.

    [Fact]
    public async Task Counting_stream_a_server_413_on_the_inner_read_raises_the_flag_and_is_rethrown_as_IOException()
    {
        var stream = new TrustedUploadLimitedReadStream(new ThrowingStream(new BadHttpRequestException("too large", 413)));

        var thrown = await Assert.ThrowsAnyAsync<IOException>(() => stream.ReadAsync(new byte[8]).AsTask());

        Assert.IsType<BadHttpRequestException>(thrown);
        Assert.True(stream.LimitExceeded);
    }

    [Fact]
    public async Task Counting_stream_any_other_IOException_leaves_the_flag_down()
    {
        var stream = new TrustedUploadLimitedReadStream(new ThrowingStream(new IOException("reset")));

        await Assert.ThrowsAsync<IOException>(() => stream.ReadAsync(new byte[8]).AsTask());

        Assert.False(stream.LimitExceeded);
        Assert.False(stream.FormatViolated);
    }

    [Fact]
    public async Task Counting_stream_one_byte_over_the_limit_raises_the_flag_and_throws_the_limit_exception()
    {
        var stream = new TrustedUploadLimitedReadStream(new MemoryStream(new byte[11]), maxBytes: 10);

        await Assert.ThrowsAsync<TrustedUploadLimitExceededException>(() => stream.CopyToAsync(Stream.Null));

        Assert.True(stream.LimitExceeded);
    }

    [Fact]
    public async Task Counting_stream_a_section_after_the_file_raises_the_format_flag()
    {
        var stream = new TrustedUploadLimitedReadStream(new MemoryStream(new byte[4]), 10, _ => Task.FromResult(true));

        await Assert.ThrowsAsync<IOException>(() => stream.CopyToAsync(Stream.Null));

        Assert.True(stream.FormatViolated);
        Assert.False(stream.LimitExceeded);
    }

    // ── §8.1 "Sınıflama kuralı" — token → 413 flag → 400 flag → service result.

    [Fact]
    public async Task Store_handler_a_failed_result_with_a_cancelled_token_is_a_cancellation_even_when_the_flag_is_up()
    {
        var (handler, _) = Handler(storeFails: true);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var content = new FlagStream { LimitExceeded = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(Command(content), cancelled.Token));
    }

    [Theory]
    [InlineData(true, false, 413)]
    [InlineData(false, true, 400)]
    [InlineData(true, true, 413)]
    [InlineData(false, false, 503)]
    public async Task Store_handler_without_cancellation_the_flags_decide_before_the_service_result(bool limit, bool format, int expected)
    {
        var (handler, _) = Handler(storeFails: true);

        var response = await handler.Handle(Command(new FlagStream { LimitExceeded = limit, FormatViolated = format }), CancellationToken.None);

        Assert.Equal(expected, response.StatusCode);
    }

    // ── r8 D-1 — None only on the trusted path; the user and in-process paths get the caller's token.

    [Fact]
    public async Task D1_metadata_write_gets_the_callers_token_on_the_user_and_in_process_paths_and_None_on_the_trusted_path()
    {
        using var caller = new CancellationTokenSource();
        var (service, objects, _) = Service();

        await service.StoreForCallerAsync(UserInput(), "c", caller.Token);
        await service.StoreAsync(UserInput(), "c", caller.Token);
        await service.StoreForTrustedConsumerAsync(TrustedInput(), Consumer(), "c", caller.Token);

        Assert.Equal(3, objects.CreateTokens.Count);
        Assert.Equal(caller.Token, objects.CreateTokens[0]);
        Assert.Equal(caller.Token, objects.CreateTokens[1]);
        Assert.False(objects.CreateTokens[2].CanBeCanceled);
    }

    [Fact]
    public async Task D1_mark_and_delete_get_the_callers_token_on_the_user_and_in_process_paths_and_None_on_the_trusted_path()
    {
        using var caller = new CancellationTokenSource();
        var (service, objects, storage) = Service();
        objects.Rows.Add(Row(Guid.NewGuid(), module: null));
        objects.Rows.Add(Row(Guid.NewGuid(), module: null));
        objects.Rows.Add(Row(Guid.NewGuid(), module: "MOD-0217"));

        await service.CompensateForCallerAsync(objects.Rows[0].ContentId, "c", caller.Token);
        await service.CompensateAsync(objects.Rows[1].ContentId, "c", caller.Token);
        await service.CompensateForTrustedConsumerAsync(objects.Rows[2].ContentId, Consumer(), "c", caller.Token);

        Assert.Equal(new[] { caller.Token, caller.Token }, objects.MarkTokens.Take(2));
        Assert.Equal(new[] { caller.Token, caller.Token }, storage.DeleteTokens.Take(2));
        Assert.False(objects.MarkTokens[2].CanBeCanceled);
        Assert.False(storage.DeleteTokens[2].CanBeCanceled);
    }

    [Fact]
    public void Access_policy_hides_a_consumer_row_from_the_user_path_and_binds_the_trusted_path_to_module_and_scope()
    {
        var permissions = new Mock<IActorPermissionContext>();
        permissions.Setup(p => p.Has(It.IsAny<string>())).Returns(true);
        var consumerRow = Row(Guid.NewGuid(), "MOD-0217");
        var userRow = Row(Guid.NewGuid(), null);

        Assert.False(DocumentRepositoryAccessPolicy.CanRead(permissions.Object, consumerRow, "someone"));
        Assert.False(DocumentRepositoryAccessPolicy.CanCompensate(permissions.Object, consumerRow, "someone"));
        Assert.True(DocumentRepositoryAccessPolicy.CanRead(permissions.Object, userRow, "someone"));
        Assert.True(DocumentRepositoryAccessPolicy.IsVisibleToTrustedConsumer(consumerRow, "MOD-0217", ["Documents"]));
        Assert.False(DocumentRepositoryAccessPolicy.IsVisibleToTrustedConsumer(consumerRow, "MOD-0999", ["Documents"]));
        Assert.False(DocumentRepositoryAccessPolicy.IsVisibleToTrustedConsumer(consumerRow, "MOD-0217", ["Templates"]));
        Assert.False(DocumentRepositoryAccessPolicy.IsVisibleToTrustedConsumer(userRow, "MOD-0217", ["Documents"]));
    }

    // ── helpers

    private static TrustedRepositoryConsumer Consumer() =>
        new(Client, "Diten.MDM", TrustedServiceTokenValidationExtensions.DocumentRepositoryRequiredAudience, "MOD-0217", ["Documents"]);

    private static StoreRepositoryObjectInput UserInput() => new(
        ContentStorageScope.Documents, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "a.pdf", "application/pdf", new MemoryStream([1]));

    private static StoreTrustedRepositoryObjectInput TrustedInput(Stream? content = null) => new(
        ContentStorageScope.Documents, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "a.pdf", "application/pdf", content ?? new MemoryStream([1]));

    private static StoreTrustedRepositoryObjectCommand Command(Stream content) => new(TrustedInput(content), Consumer(), Guid.NewGuid());

    private static RepositoryObject Row(Guid contentId, string? module) => new()
    {
        TenantId = Tenant,
        ContentId = contentId,
        StorageProvider = "fake",
        ObjectKey = "k/" + contentId,
        Scope = "Documents",
        FileName = "a.pdf",
        MediaType = "application/pdf",
        Checksum = "00",
        TrustedConsumerModule = module
    };

    private static (DocumentRepositoryService Service, FakeObjects Objects, FakeStorage Storage) Service(bool storeFails = false)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(Tenant);
        var permissions = new Mock<IActorPermissionContext>();
        permissions.Setup(p => p.Has(It.IsAny<string>())).Returns(true);
        var objects = new FakeObjects();
        var storage = new FakeStorage(storeFails);
        var service = new DocumentRepositoryService(
            storage, objects, tenant, new Diten.Platform.Application.Tests.Tasks.FakeCurrentUserContext(Guid.NewGuid()),
            NullLogger<DocumentRepositoryService>.Instance, permissions.Object);
        return (service, objects, storage);
    }

    private static (StoreTrustedRepositoryObjectHandler Handler, FakeObjects Objects) Handler(bool storeFails)
    {
        var (service, objects, _) = Service(storeFails);
        return (new StoreTrustedRepositoryObjectHandler(service), objects);
    }

    private sealed class FakeObjects : IRepositoryObjectRepository
    {
        public List<RepositoryObject> Rows { get; } = [];
        public List<CancellationToken> CreateTokens { get; } = [];
        public List<CancellationToken> MarkTokens { get; } = [];

        public Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default)
        {
            CreateTokens.Add(ct);
            Rows.Add(repositoryObject);
            return Task.FromResult(repositoryObject);
        }

        public Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.ContentId == contentId && !r.IsDeleted));

        public Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepositoryObject>>([]);

        public Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default)
        {
            MarkTokens.Add(ct);
            var row = Rows.First(r => r.ContentId == contentId);
            row.IsDeleted = true;
            return Task.FromResult(true);
        }

        public Task<RepositoryObject?> GetByContentIdIncludingCompensatedAsync(Guid contentId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.ContentId == contentId));
    }

    private sealed class FakeStorage(bool storeFails) : IContentStorageGateway
    {
        public List<CancellationToken> DeleteTokens { get; } = [];

        public Task<Response<ContentStoreResult>> StoreAsync(ContentStoreRequest request, CancellationToken ct = default) =>
            Task.FromResult(storeFails
                ? Response<ContentStoreResult>.Fail("down", 503, DocumentRepositoryReasonCodes.StorageUnavailable)
                : Response<ContentStoreResult>.Success(new ContentStoreResult(
                    Guid.NewGuid(), "fake", "k/" + Guid.NewGuid(), request.FileName, "application/pdf", 1, "00")));

        public Task<Response<ContentStreamResult>> OpenReadAsync(string storageProvider, string objectKey, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> TryDeleteAsync(string storageProvider, string objectKey, CancellationToken ct = default)
        {
            DeleteTokens.Add(ct);
            return Task.FromResult(true);
        }
    }

    private sealed class FlagStream : MemoryStream, ITrustedUploadLimitSignal
    {
        public bool LimitExceeded { get; init; }
        public bool FormatViolated { get; init; }
    }

    private sealed class ThrowingStream(Exception exception) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(exception);
    }

    private sealed class RecordingAuditService : IAuditService
    {
        public List<AuditAppendRequest> Requests { get; } = [];

        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(AuditAppendResult.Queued("key"));
        }
    }
}
