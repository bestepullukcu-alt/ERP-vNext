# Logging & Observability

## Logging
- Structured log kullan (key/value).
- TenantId’yi log alanı olarak yaz (PII yok, sadece GUID).
- Request body’yi default loglama.

## Error handling
- Global exception handling -> ProblemDetails.
- Trace/correlation id ekle.
