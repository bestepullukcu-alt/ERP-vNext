# TEST-HIZ-01 · Platform: yönlendirme testinin port yarışı (2026-10-26)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). Düzeltilmez; yeni ölçüm yeni kayıt olur.
> **İstek:** CT · Setup, 2026-10-26. Kırmızıyı bulan "Yönetici · Ofis", doğrulayan CT · Setup.
> Yerel koşular yük kuralıyla yapıldı: 1 dk yükü 12'nin üstündeyse bekle; tek proje, `--filter`.

## 1. Hata

- **Koşu:** #233 merge'ünden sonra main'de service-tests, run 40344684765 @04c8fb4cf, platform-app. 8.721 test geçti, 1 test kırmızı.
- **Test:** `AuthInternalClientsRedirectTests.A_redirect_answer_is_never_followed_with_the_credential(caller: "IUserReferenceValidator")`
- **Hata:** `System.Net.HttpListenerException: Address already in use`.
- **Yığın:** `RedirectingServer.Dispose()` → `HttpListener.Close()` → `HttpEndPointManager.RemoveListener` → `RemovePrefixInternal` → `GetEPListener`. Yani hata başlangıçta değil, **kapanışta**.

## 2. Kök neden: ölçüm

Ayrı bir deneme programı (`net8.0`, macOS; Linux'taki managed `HttpListener` ile aynı uygulama) şu sonuçları verdi:

| # | Durum | Sonuç |
|---|---|---|
| 1 | `Stop()` + `Close()`; `Stop()`'tan sonra port başka bir sokete geçiyor | `HttpListenerException: Address already in use` (CI ile aynı) |
| 2 | `Stop()` + `Close()`; port boş | Hata yok. `Close()` portu sessizce yeniden bağlıyor |
| 3 | Yalnız `Close()` | Hata yok, port sonra boş |
| 4 | Port bulunup bırakıldıktan sonra, `Start()`'tan önce başka bir sokete geçiyor | Aynı hata |

**Açıklama:**
- `Stop()` uç nokta dinleyicisini kaldırıyor.
- Ardından gelen `Close()`, öneki kaldırmak için önce aynı porta yeniden bağlanıyor (`GetEPListener` uç noktayı yoksa yaratıyor).
- Paralel çalışmada o port bu arada başka bir sokete geçmişse (giden bir bağlantının yerel portu ya da başka bir testin dinleyicisi) kapanış hata veriyor.
- Başlangıçtaki "boş port bul, bırak, bağlan" adımında da aynı tür pencere var.

## 3. Düzeltme (yalnız test kodu)

- `RedirectingServer` artık doğrudan `127.0.0.1:0`'a bağlanan gerçek bir Kestrel; bağlanılan port başladıktan sonra `IServerAddressesFeature`'dan okunuyor.
- Kapanış tek adım (`DisposeAsync`), yeniden bağlanma yok.
- Desen repoda zaten var: `AccountKindAcceptanceHost` içindeki `PlatformLoginSettingsStub`.
- Sunucunun davranışı aynı: her isteğe aynı host'ta `/trap`'e 302, sayaçlar aynı. Assert'ler değişmedi.

## 4. Ölçüm (önce / sonra)

| Koşu | Yük | Sonuç |
|---|---:|---|
| Önce: sınıf | 5,14 | 12/12 yeşil (101 ms) |
| Önce: sabotaj | 7,78 | 12/12 kırmızı |
| Sonra: sınıf | 6,56 | 12/12 yeşil (528 ms) |
| Sonra: aynı sabotaj | 10,92 | 12/12 kırmızı; kırmızı adlar önceyle birebir aynı (`diff` boş) |
| Sonra: sınıf × 10 | 7,7–10,2 | 10/10 yeşil (455–508 ms) |

- **Sabotaj:** `InternalHttpClients.CreatePrimaryHandler` içinde `AllowAutoRedirect = false` → `true`. Testin koruduğu kural bu. Dosya sonra bayt bayt geri yüklendi.
- **Maliyet:** sınıf yaklaşık 0,4 sn uzadı, çünkü her durum için Kestrel başlıyor.

## 5. Açık kalan (bu işin kapsamı dışında)

Aynı "boş port bul, bırak, sonra bağla" deseni 19 dosyada daha var; çoğu mongod portu için. Mongod da aynı tür pencereye açık. Ayrı karar.
