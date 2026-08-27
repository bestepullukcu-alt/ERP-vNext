using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.ManagementGovernanceService.Application.Behaviors.ProcessModeling;

public sealed class ValidationBehavior<TRequest,TResponse>(IEnumerable<IValidator<TRequest>> validators):IPipelineBehavior<TRequest,TResponse> where TRequest:notnull
{ public async Task<TResponse> Handle(TRequest request,RequestHandlerDelegate<TResponse> next,CancellationToken ct){var failures=validators.SelectMany(v=>v.Validate(new ValidationContext<TRequest>(request)).Errors).Where(x=>x is not null).ToArray();if(failures.Length>0)throw new ValidationException(failures);return await next();} }
public sealed class LoggingBehavior<TRequest,TResponse>(ILogger<LoggingBehavior<TRequest,TResponse>> logger):IPipelineBehavior<TRequest,TResponse> where TRequest:notnull
{ public async Task<TResponse> Handle(TRequest request,RequestHandlerDelegate<TResponse> next,CancellationToken ct){logger.LogInformation("Handling {Request}",typeof(TRequest).Name);return await next();} }
public sealed class ExceptionHandlingBehavior<TRequest,TResponse>(ILogger<ExceptionHandlingBehavior<TRequest,TResponse>> logger):IPipelineBehavior<TRequest,TResponse> where TRequest:notnull
{ public async Task<TResponse> Handle(TRequest request,RequestHandlerDelegate<TResponse> next,CancellationToken ct){try{return await next();}catch(OperationCanceledException){throw;}catch(Exception ex){logger.LogError(ex,"Unhandled {Request}",typeof(TRequest).Name);throw;}} }
public sealed class PerformanceBehavior<TRequest,TResponse>(ILogger<PerformanceBehavior<TRequest,TResponse>> logger):IPipelineBehavior<TRequest,TResponse> where TRequest:notnull
{ public async Task<TResponse> Handle(TRequest request,RequestHandlerDelegate<TResponse> next,CancellationToken ct){var sw=Stopwatch.StartNew();var r=await next();if(sw.ElapsedMilliseconds>500)logger.LogWarning("Slow {Request} {Elapsed}",typeof(TRequest).Name,sw.ElapsedMilliseconds);return r;} }
