---
status: approved
decided: 2026-10-26
module: MOD-0230
work_packages: WP-0230-DL4A, WP-0230-DL3C
---

# PVG-MOD0230-DALGA3-01 — Üçüncü dalga planı (WP-0230-DL4A, WP-0230-DL3C)

> **Karar sahibi:** sahip ("dalga3-plan", 2026-10-26, PVG Control Tower sohbeti).
> **İsteyen:** CT-Setup, sahibin "sen ver" talimatıyla.
> **Kayıt:** PVG Control Tower.
> **Paket:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`. Revizyon 10 onaylı; satırlar `830c6c1a5`'e göredir.

## Durum

| Dilim | Bugünkü durumu | Neyi bekliyor |
|---|---|---|
| DL-2 | A main'de; B'nin PR'ı açık (#226) | — |
| DL-3 | A main'de; B ölçümde | Kalan AC-10 için OD-PVG-6 |
| DL-8 | INTEGRATED | E3 kabul kaydı için kayıtlı canlı deneme (sahip: "dl8-sonra") |
| DL-4 | Bir kısmı bugün yapılabilir | AC-5 için OD-PVG-2 ile Enterprise Architecture ve MOD-0018 onayları; AC-6 için OD-PVG-3 ile MOD-0041 / Ops onayı |
| DL-5 | Bekliyor | OD-PVG-9, K4 paylaşılan forwarder, MOD-0021 |
| DL-6 | Bekliyor | OD-PVG-1, PKS-001 ve MOD-0018 |
| DL-7 | Bekliyor | DL-6; AC-32 için ayrıca DL-4 AC-31 |
| DL-9 | Bekliyor | Kapalı; "Required Approval Gates"in tamamı |

Bugün yapılabilen ve ölçülebilen iki iş var: DL-4'ün dış onay beklemeyen kısmı ve K3'ün host düzeltmesi.

## Karar

| # | Karar |
|---|---|
| D3-1 | **WP-0230-DL4A** (DL-4, kısım A). Kapsam: AC-7, AC-19 ve AC-31. AC-7: yetkisiz çağıran geçersiz gövde gönderirse doğrulama kodu değil, bağlam ya da izin sonucunu alır. AC-19: API sürümü taşır; PUT, `/triage` ve `/route` beklenen sürümü gönderir; GET ve liste sürümü döndürür; çakışma 409 olur. AC-31: API'nin istek ve yanıt kayıtları revizyon 04 alanlarını ve seçim çiftini taşır. Yollar: paketin DL-4 "Yollar" alanı. Başlama koşulu: #226 (WP-0230-DL2B) main'e girmiş olmalı. Bu bağımlılığın nedeni, AC-31'in DL-2 alanlarını gerektirmesidir. |
| D3-2 | **WP-0230-DL4B** (DL-4, kısım B). Kapsam: AC-5 ve AC-6. Kuyruğa şimdi açılmaz; OD-PVG-2 ve OD-PVG-3'ün sahip onaylarıyla açılır. |
| D3-3 | **WP-0230-DL3C** (DL-3, kısım C). Kapsam: K3 host düzeltmesi. `ASPNETCORE_ENVIRONMENT` boş dize ise Production sayılır (`Program.cs:14`, `PvgServiceApiHost.cs:41`, `:153`). Bir de DL3A'da açık kalan `Program`'ın `ASPNETCORE_URLS` okumasının korunması. Yollar: paketin DL-3 "Yollar" alanı. Başlama koşulu: WP-0230-DL3B main'de olmalı, çünkü iki iş aynı test dosyasına dokunuyor. AC-10 (OD-PVG-6) kapsam dışıdır. |
| D3-4 | **Sıra:** DL4A ile DL3C'nin yolları ayrık değil. İkisi de Api test projesine dokunuyor. DL-4'ün test dosyası belirlenince ikisi K5 kuralıyla paralel gidebilir: ikinci biten, origin/main'i birleştirip kontrolleri yeniden koşar. Aksi hâlde DL3C önce gider. Karar prompt'lar yazılırken verilir ve DoR kaydına yazılır. |
| D3-5 | **Prompt zamanı:** İki prompt, bağlı oldukları iş main'e girdikten sonra yazılır. Expected HEAD birleşmeden sonraki main olur; sebebi `feedback-no-pack-change-before-dispatch` dersi. Prompt'lar yine bağımsız incelemeden ve dağıtım kapısından geçer. |
| D3-6 | **DL2B'den kalan test boşluğu:** ürün satırı sırasının ChangedFields'ta ölçülmemesi. Bu test DL-2 yolunda. DL4A AC-31 üzerinden aynı alanlara dokunduğu için DL4A'nın kapsamına bir CT eki test olarak alınabilir. Karar prompt yazılırken verilir. |

## Gerileme değerlendirmesi

- **AC-7 sırayı değiştiriyor.** AC-7 doğrulamanın izin kapısından sonra koşmasını istiyor. Bugün doğrulama izinden önce koşuyor (G-9). DL2B bu bugünkü sırayı iki testle sabitledi: `Legacy_shaped_request_is_invalid_before_permission_gate` ve `Create_validation_runs_before_ports_and_mutation`. DL4A bu iki testi bilerek değiştirmek zorunda kalacak. Prompt bunu adıyla yazar; değişiklik paketin AC-7'sine dayanır ve gerileme sayılmaz.
- **AC-19 ve AC-31 geçici durumları kapatıyor.** DL2A'nın "sürümsüz güncelleme 409" açığı ve DL2B'nin "API create/update 400" geçici durumu bu iki AC ile kapanır.
- **DL3C davranışı daha güvenli yapıyor.** Boş ortam adı artık üretim dışı açılamaz. Canlı kapı kapalı; kullanıcıya etkisi yok.

## Kuyruk

`CT-QUEUE.tsv`'ye iki satır `PLANNED` olarak eklenir:
- `WP-0230-DL4A`, bağımlılığı `WP-0230-DL2B`.
- `WP-0230-DL3C`, bağımlılığı `WP-0230-DL3B`.

`READY` geçişi her iş için DoR kaydı ve dağıtılabilir prompt ile yapılır.
