using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.CommandHandlers;

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
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var account = await _accounts.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (account is null)
        {
            return Response<bool>.Fail("Account not found.", 404);
        }

        var referenceErrors = await AccountReferenceValidation.ValidateAsync(
            _referenceValidator, request.AccountType, request.Status, request.AccountCategory, cancellationToken);
        if (referenceErrors.Count != 0)
        {
            return Response<bool>.Fail(referenceErrors, 400);
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
        account.LogoDataUri = string.IsNullOrWhiteSpace(request.LogoDataUri) ? null : request.LogoDataUri.Trim();
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await _accounts.UpdateAsync(account, cancellationToken);
        await _audit.PublishAsync(AccountAuditEvents.Update, tenantId, account.Id, account.AccountCode, cancellationToken);
        return Response<bool>.Success(true);
    }
}
