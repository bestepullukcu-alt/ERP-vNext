# PVG dördüncü dalga — bağımsız doğrulama: WP-0230-DL4B (AC-5)

> Control Tower kaydı, 2026-10-27. Kuyrukta `DISPATCHED → VERIFIED` geçişinin kanıtıdır. Entegrasyon ya da kabul kaydı değildir.
> İş, CT dalına birleştirildi (kart S-76). Dalga sonu PR'ı ile `main`'e girecek; `INTEGRATED` geçişi o PR birleştikten sonra yazılır.

## Teslim

| | |
|---|---|
| İşçi dalı | `feature/pvg/wp-0230-dl4b-r1` |
| İş commit'i | `f2c42ec50`; HEAD^1 taban `c212053c2` |
| CT dalına birleştirme | merge commit; CT dalı birinci ebeveyn. Önceden `git merge-tree` ile ölçüldü: çakışma yok. |
| DoR | `docs/records/audits/2026-10/pvg-wave4-dor-03/DOR.md` ve `DOR-EK-01.md` (üçüncü csproj satırı) |

## Bağımsız ölçüm: KABUL EDİLEBİLİR

Ölçüm yazardan bağımsız bir CT ölçücüsü tarafından yapıldı.

- **Kapsam.**
  - Yalnız izinli 7 dosya değişti. `Program.cs`, Api.Tests csproj ve DL3C/DL3D test dosyalarının farkı boş.
  - Api csproj'una tam üç satır eklendi: JwtBearer 8.0.11, System.IdentityModel.Tokens.Jwt 8.16.0 ve Security.Secrets. Satırlar DOR-EK-01 ile uyumlu.
  - İki mevcut test dosyasında yalnız bağlam satırları değişti; madde 13(e) komutlarının dördü de 0 satır döndü.
- **Taramalar.**
  - `src` altında `X-Diten-` ya da eski başlık sabiti yok.
  - Api'de `"TenantId"` dizesi yok.
  - Commit edilmiş bir JwtSettings sırrı yok.
- **JWT doğrulaması.**
  - Kurulum CrmService ile aynı: aynı JwtSettings anahtarları, dört doğrulamanın dördü de açık, `ClockSkew = JwtValidationDefaults.ClockSkew`.
  - Ara katman sırası: Routing → Authentication → Authorization → Endpoints.
  - Altı uç noktada da `RequireAuthorization` var. Health ucu anonim.
- **Kiracı izolasyonu (ODPVG2-01/02).**
  - Kiracı `X-Tenant-Id`'den gelir ve token'daki `tenant_id` ile Guid olarak karşılaştırılır. Eşleşmezse 409 `PVG_TENANT_CONTEXT_MISMATCH` döner (G-8).
  - Aktör, token'ın subject claim'inden ve `actor_type`'tan alınır.
  - `X-Diten-*` başlıkları hiç okunmaz. N3 bunu ölçer: başka bir kiracının kaydı NOT_FOUND döner ve verisi yanıtta yer almaz.
- **Gizli bilgi (S-27).** Test anahtarları çalışma anında rastgele üretiliyor. Diff'te gerçek token, e-posta ya da şifre yok.
- **S-56.** Kiracı karşılaştırması Guid eşitliği ile yapılıyor; kültüre bağlı yeni bir karşılaştırma yok.

| Süit (yük 2,5–5,0, sıralı) | Sonuç |
|---|---|
| Api | 287/287 (262 + 25 N satırı); DL3A–DL4A testleri değişmeden yeşil |
| RegPvBase | 210 |
| CaseProcessing / MeddraCoding / SignalManagement | 23 / 40 / 43 |
| `known_failures` pvg-api / pvg-regpv-base | 0 / 0 |
| Mimari süit | Tabandaki 3 kırmızının aynısı. `JwtClockSkewGuardTests` 4/4 yeşil. |

| Ölçücünün sabotajı | Kırmızı olan |
|---|---|
| `UseAuthentication` kaldırıldı | 12 test: N3, N4, N5, N6, N7, N9. N1, N2 ve N8 beklendiği gibi yine 401. |
| `RequireAuthorization` kaldırıldı | 14 test: N1, N2, N7, N8 |
| `tenant_id` eşleşme denetimi kaldırıldı | N4'ün 3 satırı |
| `X-Diten-Tenant-Context` yeniden okundu | N3, N5 |
| `ValidateLifetime = false` | N2 "expired" |
| `ValidateAudience = false` | N2 "wrong-audience" |

**Bilgi notları:**
- JwtSettings eksikse PVG açılışı reddetmiyor; her istek 401 alıyor. CRM'den farklı, ama prompt'taki CT okumasına ve Known gaps'e uygun.
- `UnsafeResponseSamples` listesindeki `"server-tenant-context"` değeri artık işlevsiz.
- N5, aktör türünü HTTP düzeyinde ölçmüyor (DOR-EK-01).

## CT dalında birleşik ölçüm

| Süit | Sonuç |
|---|---|
| Api | 287/287 |
| RegPvBase | 210/210 |
| `known_failures` pvg-api / pvg-regpv-base | 0 / 0 |

Ölçüm sırasında yük 6,0–7,0 idi.

## Kanıt seviyesi

İş E2 kanıt üretir. DL-4 için gerekli seviye E4'tür; kabul canlı kapıya kadar açık kalır. AC-6 (hata zarfı) WP-0230-DL4C'ye kaldı.
