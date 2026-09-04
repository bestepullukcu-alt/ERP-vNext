using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Behaviors;

public sealed class ExceptionBehavior<TRequest,TResponse> : IPipelineBehavior<TRequest,TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try { return await next(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (typeof(TResponse) == typeof(Response<DwsLocalResult>))
        {
            var (code,status) = error switch
            {
                DwsNotFoundException notFound => (notFound.Code,404),
                DwsConflictException conflict => (conflict.Code,409),
                DwsValidationException validation when DwsErrors.Matrix[401].Contains(validation.Code) => (validation.Code,401),
                DwsValidationException validation when DwsErrors.Matrix[403].Contains(validation.Code) => (validation.Code,403),
                DwsValidationException validation when DwsErrors.Matrix[503].Contains(validation.Code) => (validation.Code,503),
                DwsValidationException validation => (validation.Code,400),
                InvalidOperationException invalid when invalid.Message.StartsWith("dws_",StringComparison.Ordinal) => (invalid.Message, DwsErrors.Matrix[409].Contains(invalid.Message)?409:503),
                _ => (DwsErrors.TransactionUnavailable,503)
            };
            return (TResponse)(object)Response<DwsLocalResult>.Fail(code,status);
        }
    }
}
