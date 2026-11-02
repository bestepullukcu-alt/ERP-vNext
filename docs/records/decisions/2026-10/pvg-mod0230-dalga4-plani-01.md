---
status: approved
decided: 2026-10-27
module: MOD-0230
work_packages: WP-0230-DL6, WP-0230-DL3D, WP-0230-DL4B
---

# PVG-MOD0230-DALGA4-01 — Dördüncü dalga planı

> **Karar sahibi:** sahip ("dalga4-plan", 2026-10-27, PVG Control Tower sohbeti).
> **Kayıt:** PVG Control Tower.
> **Paket:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`, revizyon 11 (`918dd660…`). Revizyon `main`'de onaylı: #261, `3a607547e`, HIGH.

## Durum

Revizyon 11 dört sahip kararını işledi. Bu kararlarla üç işin ön koşulu kalktı:

| Dilim | Açılan iş | Kaynak |
|---|---|---|
| DL-6 | AC-4: izin anahtarı ailesi | OD-PVG-1 (`pvg-mod0230-od-pvg-1-izin-01.md`) |
| DL-3 | AC-10: runtime bayrağının onay kaydına bağlanması | OD-PVG-6 (`pvg-mod0230-od-pvg-6-bayrak-01.md`) |
| DL-4 | AC-5: kimlik doğrulama, kiracı ve aktör sözleşmesi | OD-PVG-2 (`pvg-mod0230-od-pvg-2-onay-01.md`) |

Hâlâ bekleyenler:
- AC-6 (OD-PVG-3, MOD-0041 / Ops)
- DL-5 (OD-PVG-9, MOD-0021 sözleşmesi ve PVG'nin ortak iletici yolu)
- DL-2'nin sözleşmeye bağlı ölçütleri (AC-22, 24, 26, 28, 30)
- DL-7 ve DL-9

## Karar

| # | Karar |
|---|---|
| D4-1 | **WP-0230-DL6.** Kapsam: AC-4. `PvgIntakeDraftApplicationService.cs` satır 7–11'deki beş sabit `pvg.case-intake-triage.{create,update,read,triage,route}` olur ve `PvgApplicationContractShapeTests` güncellenir. Yollar paketin DL-6 "Yollar" alanından gelir. Kapsam dışı: katalog, seed, grant ve denetleyici öznitelikleri (DL-7). Ölçüm (2026-10-27): AC-4 taramasının bugün bulduğu `pvg.` satırlarının hepsi bu iki dosyada; MVC denetleyicisinde `pvg.` yok. |
| D4-2 | **WP-0230-DL3D.** Kapsam: AC-10. Bayrak `true` ise `Pvg:Runtime:ApprovalReference` zorunludur; değer yoksa üretim benzeri açılış reddedilir. Ayrıca bir CI denetimi, commit edilmiş her `ApprovalReference` değerinin depodaki onay kayıtlarında olduğunu ölçer (ODPVG6-01). Yollar paketin DL-3 "Yollar" alanından gelir. Denetimin hangi kayıt dosyasını okuyacağı prompt yazılırken ölçülür. Uygun bir dosya yoksa bu sahibe ayrı bir soru olarak gider. Bu iş biterse DL-3 diliminin bütün ölçütleri yazılmış olur. |
| D4-3 | **WP-0230-DL4B.** Kapsam: AC-5. Servis JWT'yi doğrular. Kiracıyı `X-Tenant-Id`'den alır ve token'daki `TenantId` ile eşleşmesini ister. Aktörü konu claim'inden alır. `X-Diten-*` başlıkları yok sayılır. AC-6 (OD-PVG-3) kapsam dışıdır ve ileride WP-0230-DL4C olur. |
| D4-4 | **Sıra.** DL6 ve DL3D birlikte gider; dosyaları ayrıktır. DL4B, DL3D'den sonra gider, çünkü ikisi de `Program.cs` / `PvgServiceApiHost.cs` dosyalarına dokunur (paket DL-4: "DL-3 önce"). DL4B'nin Expected HEAD'i, DL3D birleştikten sonraki CT dalı ucu olur. |
| D4-5 | **PR.** Sahibin kuralları ("tek-pr", "zorunlu olmadıkça PR yok") geçerlidir. Her işçi dalı CT dalına birleşir. Dalga sonunda tek PR açılır; `AGENTS.md` §3 port satırı da bu PR ile gider. Push için sahibin dal ve sha ile verdiği onay gerekir. |
| D4-6 | **Kanıt.** DL-6'nın gerekli kanıtı E5, DL-4'ünki E4, DL-3'ünki E3. Üç iş de E2 üretir. Satırlar canlı kapı açılana kadar `INTEGRATED` kalır. |

## Gerileme değerlendirmesi

- **DL6.** Hiçbir rol bugünkü `pvg.mod0230.intake.*` anahtarlarına sahip değil. Ad değişikliği kullanıcıyı etkilemez.
- **DL3D.** Bayrak bugün yalnız testlerde açılıyor, canlı kapı kapalı. Etki yok.
- **DL4B.** `X-Diten-*` başlıklarıyla çağıran MVC proxy, token göndermezse 401 alır. MVC ekranı DL2B'den beri zaten kaydedemiyor (K2) ve DL-7'ye kadar kullanılamaz. Bu, önceden çalışan bir akışı bozmaz. Prompt bunu açıkça yazar.

## Kuyruk

`CT-QUEUE.tsv`'ye üç satır `PLANNED` olarak eklenir:
- WP-0230-DL6, bağımlılık WP-0230-DL4A.
- WP-0230-DL3D, bağımlılık WP-0230-DL3C.
- WP-0230-DL4B, bağımlılık WP-0230-DL3D.
