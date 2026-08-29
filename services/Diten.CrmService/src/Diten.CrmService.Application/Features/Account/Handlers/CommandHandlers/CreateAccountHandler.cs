using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainAccount = Diten.CrmService.Domain.Entities.Account;
using DomainExternalRef = Diten.CrmService.Domain.Entities.AccountExternalReference;

namespace Diten.CrmService.Application.Features.Account.Handlers.CommandHandlers;

public sealed class CreateAccountHandler : IRequestHandler<CreateAccountCommand, Response<Guid>>
{
    private const string DefaultSourceSystem = "default";
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountExternalReferenceRepository _externalRefs;
    private readonly IAccountCodeGenerator _codeGenerator;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IAccountAuditPublisher _audit;

    public CreateAccountHandler(
        ITenantContext tenant,
        IAccountRepository accounts,
        IAccountExternalReferenceRepository externalRefs,
        IAccountCodeGenerator codeGenerator,
        IReferenceDataValidator referenceValidator,
        IAccountAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _externalRefs = externalRefs;
        _codeGenerator = codeGenerator;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var referenceErrors = await AccountReferenceValidation.ValidateAsync(
            _referenceValidator, request.AccountType, request.Status, request.AccountCategory, cancellationToken);
        if (referenceErrors.Count != 0)
        {
            return Response<Guid>.Fail(referenceErrors, 400);
        }

        if (request.ParentAccountId is { } parentId)
        {
            var parent = await _accounts.GetByIdAsync(tenantId, parentId, cancellationToken);
            if (parent is null)
            {
                return Response<Guid>.Fail("Parent account not found.", 404);
            }
        }

        // Pre-validate external reference uniqueness before any insert to avoid partial state.
        string? sourceSystem = null;
        if (request.ExternalReference is { } externalInput && !string.IsNullOrWhiteSpace(externalInput.ExternalId))
        {
            sourceSystem = string.IsNullOrWhiteSpace(externalInput.SourceSystem)
                ? DefaultSourceSystem
                : externalInput.SourceSystem!.Trim();

            if (await _externalRefs.ExistsBySourceExternalAsync(
                    tenantId, sourceSystem, externalInput.ExternalId.Trim(), excludeId: null, cancellationToken))
            {
                return Response<Guid>.Fail("An external reference with this SourceSystem + ExternalId already exists.", 409);
            }
        }

        string accountCode;
        if (!string.IsNullOrWhiteSpace(request.AccountCode))
        {
            accountCode = request.AccountCode!.Trim();
            if (await _accounts.ExistsByCodeAsync(tenantId, accountCode, excludeId: null, cancellationToken))
            {
                await _audit.PublishAsync(AccountAuditEvents.DuplicateRejected, tenantId, Guid.Empty, accountCode, cancellationToken);
                return Response<Guid>.Fail("AccountCode already exists for this tenant.", 409);
            }
        }
        else
        {
            try
            {
                accountCode = await _codeGenerator.GenerateAsync(tenantId, cancellationToken);
            }
            catch (AccountCodeGenerationException ex)
            {
                return Response<Guid>.Fail(ex.Message, 500);
            }
        }

        var account = new DomainAccount
        {
            TenantId = tenantId,
            AccountName = request.AccountName.Trim(),
            AccountCode = accountCode,
            AccountType = request.AccountType.Trim(),
            AccountCategory = request.AccountCategory?.Trim(),
            ParentAccountId = request.ParentAccountId,
            Status = request.Status.Trim(),
            CountryRef = request.CountryRef?.Trim(),
            CityRef = request.CityRef?.Trim(),
            DistrictRef = request.DistrictRef?.Trim(),
            AddressLine = request.AddressLine?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ResponsiblePersonName = request.ResponsiblePersonName?.Trim(),
            ResponsiblePersonPhone = request.ResponsiblePersonPhone?.Trim(),
            ResponsiblePersonEmail = request.ResponsiblePersonEmail?.Trim().ToLowerInvariant(),
            Notes = request.Notes?.Trim(),
            LogoDataUri = string.IsNullOrWhiteSpace(request.LogoDataUri) ? null : request.LogoDataUri.Trim()
        };

        await _accounts.InsertAsync(account, cancellationToken);

        if (sourceSystem is not null && request.ExternalReference is { } input)
        {
            await _externalRefs.InsertAsync(new DomainExternalRef
            {
                TenantId = tenantId,
                AccountId = account.Id,
                SourceSystem = sourceSystem,
                ExternalId = input.ExternalId.Trim(),
                SourceEntity = input.SourceEntity?.Trim(),
                DisplayName = input.DisplayName?.Trim()
            }, cancellationToken);
        }

        await _audit.PublishAsync(AccountAuditEvents.Create, tenantId, account.Id, account.AccountCode, cancellationToken);
        return Response<Guid>.Success(account.Id, 201);
    }
}
