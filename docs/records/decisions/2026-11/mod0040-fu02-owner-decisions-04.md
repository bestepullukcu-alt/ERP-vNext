# MOD-0040-FU02 — sahip kararları 04 (2026-11-10)

Paket r12 (`d29f9c5e…`, `36465315c`); bağımsız inceleme 04 tur 1 (`docs/records/reviews/MOD-0040-FU02/2026-11-10-d29f9c5e.md`, KOŞULLU).
Sahip: Beste (K2). r13 bu kararları işler.

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| F-1 | Depo arayüzüne eklenen yöntem listede olmayan test sahtelerini bozar | Yöntem ayrı dar bir arayüze taşınır (`IOrganizationReportingGraphRepository` emsali); yalnız iki Mongo deposu ve `InMemoryTenantOrganizationRepositories.cs` uygular. **XMC-001 ek onayı:** `services/Diten.Platform/tests/**/TenantOrganization/InMemoryTenantOrganizationRepositories.cs` (MOD-0288) — yalnız bu arayüzün uygulanması; başka değişiklik yok | Beste | 2026-11-10 |
| F-2 | POS'un şirketini okuma yolu | **(b)** — `CreatePositionCommandHandler` birimi ikinci kez `GetByIdAsync` ile okur; `PositionReferenceGuard.cs` değişmez; iki okuma arasında şirket değişimi §5.2 gereği kabul | Beste | 2026-11-10 |
| F-4 | Tür bağımsız kısa kod kilidinin ters yönü | DL-2 kodu yazılabilir; **A-18 (MDM kalıcı kısa kod kilidi, sözleşme N-12) kararı DL-2'nin canlıya alma kapısıdır** (Bölüm 18.1). İki yön ve "başka şirkette aynı önek" durumu pakete yazılır; Treasury CT ve MDM sahibine bildirim + kurtarma yolu | Beste | 2026-11-10 |

## Ek — 2026-11-13: A-18 / N-12 kararı (Beste, merkez; kaynak: sahip sohbet cevabı "bunu biz bu şekilde yaparız")

- **A-18 = kalıcı kilit:** bir şirkette (LegalEntity) ilk kayıt kodu üretildikten sonra o şirketin MDM kısa kodu **değiştirilemez**.
  Gerekçe: kayıt kodları kısa kodu taşır (`GMG-OUT-000003`); kısa kod değişirse ayırma defteri ile şirket arasındaki bağ kopar
  (paket Bölüm 8.13 / 8.14 "iki yön"). Bugünkü ara dönem kilidi (`LEGAL_ENTITY_SHORT_CODE_CHANGED`) kalıcı kilit gelene kadar sürer.
- **Uygulama yeri:** MDM (`UpdateLegalEntityHandler.cs:36-37`, kısa kod güncellemesi) — MDM sahibi Ali'nin modülü; XMC-001 gereği CT
  dokunmaz. Ali'ye iki istek: (1) **operasyon talimatı, hemen:** kayıt kodu üretilmiş şirketlerin kısa kodu MDM ekranından
  değiştirilmeyecek; (2) **MDM işi:** kısa kod güncellemesine kalıcı kilit (şirkette kayıt kodu varsa ret), ayrı MDM iş paketi.
- **Etki:** DL-2'nin canlıya alma kapısı (paket Bölüm 18.1) karar düzeyinde kapandı; MDM kilidi uygulanana kadar ara dönem kilidi ve
  operasyon talimatı birlikte koruma sağlar. Sözleşme N-12 satırı bir sonraki sözleşme / paket revizyonunda "karar verildi" olarak
  güncellenir. Ali'nin bilgisi ve MDM işini üstlenmesi beklenir (MDM sahibi olarak itiraz ederse karar yeniden açılır).
