using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Api.LocalTest;

public sealed class DwsLocalTestExceptionBoundary(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (TryClassify(error, out var code, out var status))
        {
            if (context.Response.HasStarted)
                throw;
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(Response<object>.Fail(code, status), context.RequestAborted);
        }
    }

    private static bool TryClassify(Exception error, out string code, out int status)
    {
        (code, status) = error switch
        {
            DwsNotFoundException notFound => (notFound.Code, 404),
            DwsConflictException conflict => (conflict.Code, 409),
            DwsValidationException validation => (validation.Code, Status(validation.Code, 400)),
            InvalidOperationException invalid when Status(invalid.Message, 0) is var mapped && mapped != 0 =>
                (invalid.Message, mapped),
            _ => (string.Empty, 0)
        };
        return status != 0;
    }

    private static int Status(string code, int fallback)
    {
        var match = DwsErrors.Matrix.SingleOrDefault(entry => entry.Value.Contains(code));
        return match.Value is null ? fallback : match.Key;
    }
}
