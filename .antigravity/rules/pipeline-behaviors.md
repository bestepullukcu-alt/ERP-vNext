---
description: "PIPELINE-001 — Diten ERP vNext MediatR Pipeline Behavior Standartları (4 Zorunlu Katman)"
---

# MediatR Pipeline Behavior Standardı (Diten ERP vNext)

Her mikroserviste aşağıdaki 4 pipeline behavior **zorunlu olarak** kurulmalıdır.
Behavior'lar `Application` katmanında yaşar — `Infrastructure`'da değil.

---

## 🔄 Yürütme Sırası (Kritik)

```
Request
  → ValidationBehavior       (1. önce doğrula)
  → LoggingBehavior          (2. isteği logla)
  → ExceptionHandlingBehavior (3. beklenmedik hataları yakala)
  → PerformanceBehavior      (4. süreyi ölç)
  → Handler
```

DI kayıt sırası bu akışı belirler — **sıra değiştirilemez**.

---

## 1. ValidationBehavior

FluentValidation kurallarını pipeline'da otomatik tetikler.
Hata varsa handler'a ulaşmadan `Response<T>.Fail()` döner.

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .Select(f => f.ErrorMessage)
            .ToList();

        if (failures.Count == 0)
            return await next();

        // Response<T>.Fail() döndürmek için reflection kullanılır
        // veya IValidationResponse marker interface'i tercih edilebilir
        var responseType = typeof(TResponse);
        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Response<>))
        {
            var innerType = responseType.GetGenericArguments()[0];
            var failMethod = typeof(Response<>)
                .MakeGenericType(innerType)
                .GetMethod("Fail", [typeof(IReadOnlyList<string>), typeof(int)])!;
            return (TResponse)failMethod.Invoke(null, [failures.AsReadOnly(), 400])!;
        }

        throw new ValidationException(failures.Select(f => new ValidationFailure("", f)));
    }
}
```

---

## 2. LoggingBehavior

Her request/response çiftini Serilog ile loglar.
Handler süresini ve başarı durumunu kaydeder.

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        _logger.LogInformation(
            "Handled {RequestName} in {ElapsedMs}ms",
            requestName,
            sw.ElapsedMilliseconds);

        return response;
    }
}
```

---

## 3. ExceptionHandlingBehavior

Handler'dan fırlayan beklenmedik exception'ları yakalar.
`Response<T>.Fail()` olarak döndürür — exception UI veya client'a sızmaz.

```csharp
public sealed class ExceptionHandlingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> _logger;

    public ExceptionHandlingBehavior(
        ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogError(ex, "Unhandled exception for {RequestName}", requestName);

            var responseType = typeof(TResponse);
            if (responseType.IsGenericType &&
                responseType.GetGenericTypeDefinition() == typeof(Response<>))
            {
                var innerType = responseType.GetGenericArguments()[0];
                var failMethod = typeof(Response<>)
                    .MakeGenericType(innerType)
                    .GetMethod("Fail", [typeof(string), typeof(int)])!;
                return (TResponse)failMethod.Invoke(null, ["An unexpected error occurred.", 500])!;
            }

            throw;
        }
    }
}
```

> **Not:** Infrastructure hatalarında (MongoDB bağlanamadı vb.) exception fırlatmak doğrudur.
> Bu behavior o exception'ı yakalar ve `500` döndürür.

---

## 4. PerformanceBehavior

Handler yürütme süresi eşiği (varsayılan: **500ms**) aşarsa uyarı loglar.
Production'da yavaş sorguları tespit etmek için kullanılır.

```csharp
public sealed class PerformanceBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int WarningThresholdMs = 500;
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(
        ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > WarningThresholdMs)
        {
            _logger.LogWarning(
                "Slow request detected: {RequestName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                typeof(TRequest).Name,
                sw.ElapsedMilliseconds,
                WarningThresholdMs);
        }

        return response;
    }
}
```

---

## 📦 DI Kayıt Şablonu (`DependencyInjection.cs` — Application katmanı)

```csharp
public static IServiceCollection AddApplicationServices(
    this IServiceCollection services)
{
    services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

    // Sıra zorunludur — değiştirme
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));

    return services;
}
```

---

## 🚫 Yasak Kullanımlar

```csharp
// ❌ Infrastructure katmanında behavior tanımlamak
// Diten.MdmService.Infrastructure/Behaviors/ValidationBehavior.cs → YANLIŞ
// Doğru yer: Diten.MdmService.Application/Behaviors/

// ❌ Behavior'ları kayıt sırasını değiştirmek
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>)); // önce
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));  // sonra → YANLIŞ

// ❌ Global exception middleware ile çakışma
// ExceptionHandlingBehavior pipeline'da varsa ayrıca GlobalExceptionHandler middleware gerekmez
// Biri seçilir — bu projede pipeline behavior tercih edilir
```

---

## ✅ Kontrol Listesi

- [ ] `Application/Behaviors/` klasörü var mı?
- [ ] 4 behavior sınıfı mevcut mu? (Validation, Logging, ExceptionHandling, Performance)
- [ ] DI kayıt sırası doğru mu? (Validation → Logging → ExceptionHandling → Performance)
- [ ] Behavior'lar `Application` katmanında mı? (`Infrastructure`'da değil)
- [ ] `ValidationBehavior` hata varsa `Response<T>.Fail()` döndürüyor mu?
- [ ] `ExceptionHandlingBehavior` beklenmedik exception'ları `500` olarak sarıyor mu?
- [ ] `PerformanceBehavior` 500ms eşiği aşıldığında `LogWarning` yazıyor mu?
