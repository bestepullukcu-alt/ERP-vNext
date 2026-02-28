# ERP Mimari Kuralları — Katmanlama

## Projeler
- <Service>.Api (veya <Service> Web API Host)
- <Service>.Application
- <Service>.Domain
- <Service>.Persistence
- <Service>.Infrastructure

## Bağımlılık kuralları (zorunlu)
- Web/API -> Application -> Domain
- Persistence/Infrastructure dış katmanlardır; Domain’e (ve gerekirse Application’a) bağımlı olabilir.
- Ters bağımlılık YASAK (Domain; Application/Web/Persistence’i referanslamaz).

## CQRS
- Controller içinde iş kuralı OLMAZ.
- Her endpoint bir MediatR Command veya Query çağırır.
- Validation handler’dan önce çalışır (pipeline/validator).

## Persistence (Mongo)
- MongoDB.Driver sadece Persistence’te.
- Repository’ler tenant filtresini otomatik uygular.

## Genel
- IO path’lerinde async + CancellationToken kullan.
- Hatalar ProblemDetails ile tek formatta dönsün.
