# MVP-3 INT3-01…03 — sahip kararları (Beste, 2026-11-24)

Kaynak: `docs/records/audits/2026-11/mvp2-mvp3-ct-handoff-01/MVP-3-DEVIR.md` §3 (satır 27-34) ve CT envanteri (2026-11-24).
MVP-3, MVP-5 ile paralel ilerler; ilk adımlar kodsuzdur.

- **INT3-01 (MOD-0193 → MOD-0290 gerçek ürün doğrulaması):** Active olmayan (taslak ya da pasif) ürün BOM'a **girmez**
  (`lifecycleStatus` ≠ Active → ret). Açık kalan, Ali'ye sorulan iki karar: PRODUCT-MASTER v1.1 (`1.1.0-draft.16`) dondurma tarihi
  ve Manufacturing → MDM doğrulama çağrısının kimliği (kullanıcı token'ı iletimi mi, Auth servis token'ı mı). Bu kararlardan sonra
  MOD-0193 için dar iş paketi (200'lük parçalama, 400 / 503 ayrımı, `getSkuUom`, istemci testleri).
- **INT3-02 (Manufacturing → merkezi denetim, BL-501):** erteleme **kalkmaz**; bağlantı diğer MVP'lerle birlikte, merkezi ekip
  tarafından MVP'lerden sonra yapılır (`730a7fa1f` kararı aynen).
- **INT3-03 (korelasyon, MOD-0040):** Blueprint MOD-0040 (Canonical ID & Correlation Standard) **yalnız standart** olarak kayda
  geçer; yeni modül kimliği açılmaz. Manufacturing'deki `CorrelationMiddleware` + `ICorrelationContext` uygulaması ve Gateway
  korelasyon kuralı (her istek Gateway'de kendi `X-Correlation-Id`'sini alır; çağıranın değeri `X-Client-Correlation-Id`) referans
  davranıştır. F-0193-09 (ad çakışması) bu kayıtla kapanır: repo satırı MOD-0040 = MOD-0288'in takma adı, Blueprint MOD-0040 =
  standart; MOD-0040-FU02 Blueprint anlamına bağlıdır. Kayıt defteri notu ve MOD-0193 satırı (port 5070, durum `done`) düzeltildi.
- **Sınır:** Kod, ortam açma ya da dağıtım izni değildir.
