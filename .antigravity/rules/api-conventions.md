# API Konvansiyonları

## Routing
- REST isimlendirme: /api/<resource>
- Çoğul isim: /api/categories

## Status Code
- 200 OK, 201 Created, 204 NoContent
- 400 BadRequest (validation / tenant header problemi)
- 401 Unauthorized (JWT yok/invalid)
- 403 Forbidden (yetki yok)
- 404 NotFound (entity yok — cross-tenant leak yapma)

## Error
- ProblemDetails standardı kullan.
- Mümkünse trace/request id ekle.
