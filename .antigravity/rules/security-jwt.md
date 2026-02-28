# Güvenlik — JWT Kuralları

## Standart
- Her servis JWT’yi kendi içinde doğrular (JwtBearer).
- Konfig placeholders kabul (Authority, Audience vs.), hardcoded secret YASAK.

## Kurallar
- Token, secret, connection string loglamak YASAK.
- POST/PUT/PATCH/DELETE endpoint’ler default [Authorize] olsun (aksi açıkça istenmedikçe).
