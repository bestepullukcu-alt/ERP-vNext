# Ağ geçidi yol aşma ölçümü — ek 01 (2026-11-02, CT · Yetki)

Ek kayıt; `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02.md` değiştirilmez. Bağımsız
inceleme ikinci tur F-10'a cevaptır: ölçülen commit, geçici fark ve ham çıktı.

- Ölçülen commit: kule dalı `chore/ct/auth-ct-01` @ `6f169f677` (origin/main `4099e3ad1` birleştirmesi). Ölçüm, paket
  commit'i `920ef81f4`'ten önce, çalışma ağacında yapıldı; ağ geçidi kodu ve testleri `6f169f677` ile aynıydı.
- Komut: `dotnet test gateway/Diten.ApiGateway.Tests --filter "FullyQualifiedName~Mod0290Fu04ProductMasterTraversalTests" --logger "console;verbosity=detailed"`
- Geçici fark (koşudan sonra geri alındı; `git status --short gateway` boş döndü). Birinci koşu:

```diff
     [InlineData("/api/product-master/%252e%252e%252f%252e%252e%252finternal/x")]
+    [InlineData("/api/product-master/%252e%252e/%252e%252e/internal/x")]
+    [InlineData("/api/product-master/skus/%252e%252e/%252e%252e/%252e%252e/internal/x")]
```

  İkinci koşu (bu iki satırın yerine):

```diff
+    [InlineData("/api/product-master/%252e%252e/internal/v1/product-master/validate")]
```

- Ham çıktı alıntısı (birinci koşu): `Toplam test sayısı: 9 · Geçti: 7 · Başarısız: 2`;
  `/api/product-master/%252e%252e/%252e%252e/internal/x → gateway 200; reached the service: /internal/x (routed /internal/x)`;
  `/api/product-master/skus/%252e%252e/%252e%252e/%252e%252e/internal/x → gateway 200; reached the service: /internal/x (routed /internal/x)`.
- Ham çıktı alıntısı (ikinci koşu): `Toplam test sayısı: 8 · Geçti: 7 · Başarısız: 1`;
  `/api/product-master/%252e%252e/internal/v1/product-master/validate → gateway 200; reached the service: /api/internal/v1/product-master/validate (routed /api/internal/v1/product-master/validate)`;
  `/api/product-master/%2e%2e/%2e%2e/internal/v1/product-master/validate → gateway 404; reached the service: nothing`;
  `/api/product-master/../../internal/v1/product-master/validate → gateway 404; reached the service: nothing`.
- Ölçülmeyen: yalnız Ocelot'lu test konağı (Program.cs'in kimlik doğrulama ve kiracı çözümü yok); diğer rotalar.
