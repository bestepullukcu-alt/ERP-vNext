using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.CommandHandlers;

/// <summary>
/// MOD-0149 account update. WP-CRM-MOBILE-REQ CRM-ACC-EDIT (§19): optimistic — the caller's <c>ExpectedVersion</c> must be
/// the stored one, checked again by the ONE conditional write (a concurrent edit loses with 409
/// <c>account_version_conflict</c>, nothing written); a deleted / foreign account is 404; an omitted logo is kept
/// (<c>RemoveLogo</c> removes it); only the fields the command carries change — the code, the parent, the external
/// references, the attributes, the creation stamp stay as stored.
/// </summary>
public sealed class UpdateAccountHandler : IRequestHandler<UpdateAccountCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IAccountAuditPublisher _audit;

    public UpdateAccountHandler(
        ITenantContext tenant,
        IAccountRepository accounts,
        IReferenceDataValidator referenceValidator,
        IAccountAuditPublisher audit)
    {
        _tenant = tenant;
        _accounts = accounts;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return AccountErrors.Fail<bool>("Tenant context is required.", AccountErrors.TenantRequired, 400);
        }

        if (request.ExpectedVersion is not { } expectedVersion)
        {
            return AccountErrors.Fail<bool>("expectedVersion (the version you read) is required.", AccountErrors.ExpectedVersionRequired, 400);
        }

        var account = await _accounts.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (account is null || account.TenantId != tenantId || account.IsDeleted)
        {
            return AccountErrors.Fail<bool>("Account not found.", AccountErrors.NotFound, 404);
        }

        if (account.Version != expectedVersion)
        {
            return VersionConflict();
        }

        var referenceErrors = await AccountReferenceValidation.ValidateAsync(
            _referenceValidator, request.AccountType, request.Status, request.AccountCategory, cancellationToken);
        if (referenceErrors.Count != 0)
        {
            return AccountErrors.Fail<bool>(referenceErrors, 400);
        }

        account.AccountName = request.AccountName.Trim();
        account.AccountType = request.AccountType.Trim();
        account.AccountCategory = request.AccountCategory?.Trim();
        account.Status = request.Status.Trim();
        account.CountryRef = request.CountryRef?.Trim();
        account.CityRef = request.CityRef?.Trim();
        account.DistrictRef = request.DistrictRef?.Trim();
        account.AddressLine = request.AddressLine?.Trim();
        account.Latitude = request.Latitude;
        account.Longitude = request.Longitude;
        account.ResponsiblePersonName = request.ResponsiblePersonName?.Trim();
        account.ResponsiblePersonPhone = request.ResponsiblePersonPhone?.Trim();
        account.ResponsiblePersonEmail = request.ResponsiblePersonEmail?.Trim().ToLowerInvariant();
        account.Notes = request.Notes?.Trim();
        if (request.RemoveLogo)
        {
            account.LogoDataUri = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.LogoDataUri))
        {
            account.LogoDataUri = request.LogoDataUri.Trim();
        }
        // else: the logo was not sent — it stays as stored (a client that never touched it never erases it)

        account.UpdatedAt = DateTimeOffset.UtcNow;

        if (!await _accounts.ReplaceIfVersionAsync(account, expectedVersion, cancellationToken))
        {
            return VersionConflict();
        }

        await _audit.PublishAsync(AccountAuditEvents.Update, tenantId, account.Id, account.AccountCode, cancellationToken);
        return Response<bool>.Success(true);
    }

    private static Response<bool> VersionConflict()
        => AccountErrors.Fail<bool>("The account was changed by someone else; reload it and apply your change again.",
            AccountErrors.VersionConflict, 409);
}
