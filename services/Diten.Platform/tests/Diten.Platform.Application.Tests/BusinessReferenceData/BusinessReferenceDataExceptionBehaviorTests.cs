using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataExceptionBehaviorTests
{
    [Theory]
    [InlineData("reference_data_set_not_found")]
    [InlineData("")]
    public async Task Key_not_found_is_returned_as_controlled_404_instead_of_escaping_as_500(string message)
    {
        var behavior = new BusinessReferenceDataExceptionBehavior<TestRequest, Response<string>>(
            NullLogger<BusinessReferenceDataExceptionBehavior<TestRequest, Response<string>>>.Instance);

        var response = await behavior.Handle(
            new TestRequest(),
            () => throw new KeyNotFoundException(message),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(
            string.IsNullOrWhiteSpace(message) ? "not_found" : message,
            Assert.Single(response.Errors));
    }

    private sealed record TestRequest : IRequest<Response<string>>, IBusinessReferenceDataRequest;
}
