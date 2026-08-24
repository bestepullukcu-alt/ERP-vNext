using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleRegistration;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleRegistration;

public sealed class InternalModuleRegistrationControllerTests
{
    [Fact]
    public async Task Rejected_credential_never_dispatches_reconcile()
    {
        var mediator = new Mock<IMediator>();
        var controller = BuildController(mediator, ModuleRegistrationAuthenticationResult.Rejected);

        var result = await controller.RegisterManifest(ProductManifest(), CancellationToken.None);

        Assert.Equal(401, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        mediator.Verify(x => x.Send(It.IsAny<RegisterModuleManifestCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rejection_response_never_echoes_credential_secret()
    {
        var mediator = new Mock<IMediator>();
        var logger = new RecordingLogger<InternalModuleRegistrationController>();
        var controller = BuildController(mediator, ModuleRegistrationAuthenticationResult.Rejected, logger: logger);
        var secret = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        controller.Request.Headers["X-Module-Registration-Credential"] = secret;

        var result = await controller.RegisterManifest(ProductManifest(), CancellationToken.None);
        var responseJson = JsonSerializer.Serialize(Assert.IsAssignableFrom<ObjectResult>(result).Value);

        Assert.DoesNotContain(secret, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain(logger.Messages, message => message.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Successful_registration_log_and_response_never_contain_supplied_secret()
    {
        var mediator = SuccessfulMediator();
        var logger = new RecordingLogger<InternalModuleRegistrationController>();
        var controller = BuildController(mediator, new(true, "DITENMDMSERVICE"), logger: logger);
        var secret = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        controller.Request.Headers["X-Module-Registration-Credential"] = secret;

        var result = await controller.RegisterManifest(ProductManifest(), CancellationToken.None);
        var responseJson = JsonSerializer.Serialize(Assert.IsAssignableFrom<ObjectResult>(result).Value);

        Assert.DoesNotContain(secret, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain(logger.Messages, message => message.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Protected_product_never_falls_back_to_valid_shared_internal_key()
    {
        var mediator = new Mock<IMediator>();
        var sharedSecret = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var controller = BuildController(mediator, ModuleRegistrationAuthenticationResult.Rejected, sharedSecret);
        controller.Request.Headers["X-Internal-Api-Key"] = sharedSecret;

        var result = await controller.RegisterManifest(ProductManifest(), CancellationToken.None);

        Assert.Equal(401, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        mediator.Verify(x => x.Send(It.IsAny<RegisterModuleManifestCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Authenticated_mdm_owner_is_added_server_side_to_command()
    {
        var mediator = SuccessfulMediator();
        var controller = BuildController(mediator, new(true, "DITENMDMSERVICE"));

        await controller.RegisterManifest(ProductManifest() with { Service = "untrusted-body-value" }, CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<RegisterModuleManifestCommand>(c => c.TrustedProducerOwnerCode == "DITENMDMSERVICE"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Legal_entity_keeps_existing_manifest_semantics_after_credential_transport()
    {
        var mediator = SuccessfulMediator();
        var controller = BuildController(mediator, new(true, "DITENMDMSERVICE"));
        var legalEntity = ProductManifest() with { ModuleCode = "legal-entity", ModuleName = "LegalEntity" };

        await controller.RegisterManifest(legalEntity, CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<RegisterModuleManifestCommand>(c => c.Manifest == legalEntity),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Authenticated_mdm_credential_cannot_register_an_unknown_module_mapping()
    {
        var mediator = SuccessfulMediator();
        var controller = BuildController(mediator, new(true, "DITENMDMSERVICE"));
        var unknown = ProductManifest() with { ModuleCode = "unknown-module" };
        controller.Request.Headers["X-Module-Registration-Credential-Id"] = $"test-{Guid.NewGuid():N}";

        var result = await controller.RegisterManifest(unknown, CancellationToken.None);

        Assert.Equal(401, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        mediator.Verify(x => x.Send(It.IsAny<RegisterModuleManifestCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Non_mdm_manifest_preserves_legacy_internal_key_transport()
    {
        var mediator = SuccessfulMediator();
        var sharedSecret = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var controller = BuildController(mediator, ModuleRegistrationAuthenticationResult.Rejected, sharedSecret);
        controller.Request.Headers["X-Internal-Api-Key"] = sharedSecret;
        var manifest = ProductManifest() with { ModuleCode = "developer-enablement-module" };

        await controller.RegisterManifest(manifest, CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<RegisterModuleManifestCommand>(c => c.Manifest == manifest && c.TrustedProducerOwnerCode == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static InternalModuleRegistrationController BuildController(
        Mock<IMediator> mediator,
        ModuleRegistrationAuthenticationResult authentication,
        string? sharedSecret = null,
        ILogger<InternalModuleRegistrationController>? logger = null)
    {
        var authenticator = new Mock<IModuleRegistrationCredentialAuthenticator>();
        authenticator.Setup(x => x.Authenticate(It.IsAny<string?>(), It.IsAny<string?>())).Returns(authentication);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthService:InternalApiKey"] = sharedSecret ?? Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            })
            .Build();
        var controller = new InternalModuleRegistrationController(
            mediator.Object,
            authenticator.Object,
            configuration,
            new TenantContext(),
            logger ?? NullLogger<InternalModuleRegistrationController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return controller;
    }

    private static Mock<IMediator> SuccessfulMediator()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<RegisterModuleManifestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<ModuleManifestReconcileResult>.Success(
                data: new ModuleManifestReconcileResult("PRODUCT-ITEM-SKU-MASTER", "created", 0, 0, 0, []),
                statusCode: 200));
        return mediator;
    }

    private static ModuleManifestDocument ProductManifest() =>
        new("product-item-sku-master", "ProductItemSkuMaster", "Product", "MasterDataManagement", "DitenMdmService", "1.0.0", true, 10, []);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
