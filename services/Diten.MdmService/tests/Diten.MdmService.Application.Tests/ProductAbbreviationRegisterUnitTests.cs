using System.Globalization;
using System.Reflection;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationRegisterUnitTests
{
    [Theory]
    [InlineData(" abc ", "ABC")]
    [InlineData("XYZ", "XYZ")]
    public void Grammar_normalizes_invariant_trim_and_uppercase(string input, string expected)
    {
        using var _ = new TemporaryCulture("tr-TR");

        Assert.True(ProductAbbreviationNormalizer.TryNormalize(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("ABCD")]
    [InlineData("A B")]
    [InlineData("A1B")]
    [InlineData("İBC")]
    [InlineData("ÅBC")]
    public void Grammar_rejects_every_non_three_ascii_letter_result(string input)
        => Assert.False(ProductAbbreviationNormalizer.TryNormalize(input, out _));

    [Fact]
    public void Lifecycle_is_closed_and_has_no_corrected_state()
    {
        Assert.Equal(
            ["REQUESTED", "ACTIVE", "REJECTED", "CANCELLED", "RETIRED"],
            Enum.GetNames<ProductAbbreviationLifecycleStatus>());
        Assert.DoesNotContain("CORRECTED", Enum.GetNames<ProductAbbreviationLifecycleStatus>());
    }

    [Fact]
    public async Task Authorization_denial_happens_before_any_repository_access_or_write()
    {
        var actor = Actor(
            subject: "human-1",
            actorType: "service",
            permissions: new HashSet<string>(StringComparer.Ordinal) { ProductAbbreviationPermissions.Request });
        var workflow = CreateWorkflow(actor, Proxy<IProductAbbreviationRegisterRepository>());
        var handler = new RequestProductAbbreviationAllocationHandler(workflow);

        var response = await handler.Handle(
            new RequestProductAbbreviationAllocationCommand(Guid.NewGuid(), "ABC", "request-1"),
            default);

        Assert.False(response.IsSuccessful);
        Assert.Equal("ABBREVIATION_ACTOR_NOT_DIRECT_TENANT_HUMAN", Assert.Single(response.Errors));
    }

    [Fact]
    public async Task Missing_canonical_subject_denial_happens_before_any_repository_access_or_write()
    {
        var actor = Actor(
            subject: string.Empty,
            actorType: "tenant_user",
            permissions: new HashSet<string>(StringComparer.Ordinal) { ProductAbbreviationPermissions.Request });
        var workflow = CreateWorkflow(actor, Proxy<IProductAbbreviationRegisterRepository>());
        var handler = new RequestProductAbbreviationAllocationHandler(workflow);

        var response = await handler.Handle(
            new RequestProductAbbreviationAllocationCommand(Guid.NewGuid(), "ABC", "request-invalid-subject"),
            default);

        Assert.False(response.IsSuccessful);
        Assert.Equal("ABBREVIATION_ACTOR_NOT_DIRECT_TENANT_HUMAN", Assert.Single(response.Errors));
    }

    [Fact]
    public async Task Different_direct_tenant_user_cannot_cancel_request_owned_by_another_subject()
    {
        var owner = Guid.NewGuid().ToString("D");
        var register = new RegisterSpy
        {
            Entry = new ProductAbbreviationRegisterEntry
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                GlobalProductId = Guid.NewGuid(),
                NormalizedAbbreviation = "OWN",
                LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
                RequestedByCanonicalSubjectId = owner,
                Version = 0
            }
        };
        var actor = Actor(
            Guid.NewGuid().ToString("D"),
            "tenant_user",
            new HashSet<string>(StringComparer.Ordinal) { ProductAbbreviationPermissions.Cancel });
        var handler = new CancelProductAbbreviationAllocationHandler(CreateWorkflow(actor, register));

        var response = await handler.Handle(
            new CancelProductAbbreviationAllocationCommand(register.Entry.Id, 0, "cancel-not-owner"),
            default);

        Assert.False(response.IsSuccessful);
        Assert.Equal("ABBREVIATION_CANCEL_NOT_REQUEST_OWNER", Assert.Single(response.Errors));
        Assert.Equal(0, register.TransitionCalls);
    }

    [Fact]
    public async Task Maker_cannot_approve_own_request_and_transition_is_not_called()
    {
        var maker = "canonical-human";
        var register = new RegisterSpy
        {
            Entry = new ProductAbbreviationRegisterEntry
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                GlobalProductId = Guid.NewGuid(),
                NormalizedAbbreviation = "ABC",
                LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
                RequestedByCanonicalSubjectId = maker,
                Version = 0
            }
        };
        var actor = Actor(
            maker,
            "tenant_user",
            new HashSet<string>(StringComparer.Ordinal) { ProductAbbreviationPermissions.Approve });
        var workflow = CreateWorkflow(actor, register);
        var handler = new ApproveProductAbbreviationAllocationHandler(workflow);

        var response = await handler.Handle(
            new ApproveProductAbbreviationAllocationCommand(register.Entry.Id, 0, "approve-1"),
            default);

        Assert.False(response.IsSuccessful);
        Assert.Equal("ABBREVIATION_MAKER_CHECKER_VIOLATION", Assert.Single(response.Errors));
        Assert.Equal(0, register.TransitionCalls);
    }

    private static ProductAbbreviationWorkflow CreateWorkflow(
        IProductAbbreviationActorContext actor,
        IProductAbbreviationRegisterRepository register)
        => new(
            register,
            Proxy<IProductAbbreviationAllocationLedgerRepository>(),
            Proxy<IProductAbbreviationHistoryRepository>(),
            Proxy<IGlobalProductRepository>(),
            actor,
            new ProductAbbreviationAuthorization(actor));

    private static TestActorContext Actor(
        string subject,
        string actorType,
        IReadOnlySet<string> permissions)
        => new(Guid.NewGuid(), true, true, actorType, subject, permissions, "correlation");

    private static T Proxy<T>() where T : class => DispatchProxy.Create<T, ThrowingProxy>();

    private class ThrowingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException($"Repository access was not expected: {targetMethod?.Name}");
    }

    private sealed record TestActorContext(
        Guid Tenant,
        bool TenantResolved,
        bool Authenticated,
        string ActorTypeValue,
        string Subject,
        IReadOnlySet<string> Permissions,
        string Correlation) : IProductAbbreviationActorContext
    {
        public Guid TenantId => Tenant;
        public bool TenantIsResolved => TenantResolved;
        public bool IsAuthenticated => Authenticated;
        public string ActorType => ActorTypeValue;
        public string CanonicalHumanSubjectId => Subject;
        public IReadOnlySet<string> GrantedPermissions => Permissions;
        public string CorrelationId => Correlation;
    }

    private sealed class RegisterSpy : IProductAbbreviationRegisterRepository
    {
        public required ProductAbbreviationRegisterEntry Entry { get; init; }
        public int TransitionCalls { get; private set; }

        public Task<ProductAbbreviationRegisterEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductAbbreviationRegisterEntry?>(id == Entry.Id ? Entry : null);
        public Task<ProductAbbreviationRegisterEntry?> GetByAllocationIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductAbbreviationRegisterEntry?>(null);
        public Task<ProductAbbreviationRegisterEntry?> GetActiveByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductAbbreviationRegisterEntry?>(null);
        public Task<ProductAbbreviationRegisterEntry?> ResolveActiveAsync(string normalizedAbbreviation, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductAbbreviationRegisterEntry?>(null);
        public Task<ProductAbbreviationRegisterWriteResult> InsertRequestedAsync(ProductAbbreviationRegisterEntry entry, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProductAbbreviationRegisterWriteResult> TransitionAsync(Guid id, int expectedVersion, ProductAbbreviationLifecycleStatus expectedStatus, ProductAbbreviationLifecycleStatus targetStatus, string decisionActor, string idempotencyKey, string? reason, DateTimeOffset decidedAtUtc, CancellationToken cancellationToken = default)
        {
            TransitionCalls++;
            throw new InvalidOperationException("Transition must not be called for maker self-approval.");
        }
        public Task<ProductAbbreviationRegisterWriteResult> RequestRetirementAsync(Guid id, int expectedVersion, string retirementRequestId, string makerSubjectId, string idempotencyKey, string? reason, DateTimeOffset requestedAtUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProductAbbreviationRegisterWriteResult> ClearRetirementRequestAsync(Guid id, int expectedVersion, string retirementRequestId, string checkerSubjectId, string idempotencyKey, string? reason, DateTimeOffset decidedAtUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProductAbbreviationRegisterWriteResult> ReconcileCorrectionApprovalAsync(Guid formerEntryId, int expectedFormerVersion, Guid replacementEntryId, int expectedReplacementVersion, string checkerSubjectId, string idempotencyKey, string? reason, DateTimeOffset decidedAtUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TemporaryCulture : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        public TemporaryCulture(string culture)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
