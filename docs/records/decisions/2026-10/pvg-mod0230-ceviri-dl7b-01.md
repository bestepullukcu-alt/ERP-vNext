---
status: approved
decided: 2026-10-28
module: MOD-0230
work_packages: WP-0230-DL7B
---

# PVG-MOD0230-CEVIRI-01 — Ekran metinlerinin çevirisi DL7B'ye katılır

> **Karar sahibi:** sahip ("ceviri-dl7b", 2026-10-28, PVG Control Tower sohbeti).
> **Kayıt:** PVG Control Tower.
> **Dayanak:** `docs/records/audits/2026-10/pvg-wave5-verify-01/VERIFY-DL7A.md`, ORTA bulgu.

## Durum

`CaseIntakeTriageIndex.*.resx` yedi dilde aynı 88 anahtarı taşıyor; AC-15'in "aynı anahtar kümesi" koşulu bu yüzden karşılanıyor. Ancak tr, fr, es, zh, ar ve ru dosyalarında 88 değerin 87'si İngilizce kopya. Yalnız DL7A'nın eklediği `VersionConflict` çevrilmiş.

Bu borç DL7A'dan önce de vardı. DL7A tabanında (`5f90c216a`) tr dosyasının 87 anahtarı da İngilizceydi. Yani bir gerileme değil. Yine de `localization-standard`'ın placeholder yasağına ve Çekirdek madde 4'e ("bir dil eksikse iş bitmemiştir") aykırı.

## Karar

| # | Karar |
|---|---|
| C-1 | Mevcut anahtarların altı dile çevirisi WP-0230-DL7B'nin kapsamına alınır. DL7B'nin kendi yeni etiketleriyle birlikte yapılır. |
| C-2 | Ayrı bir çeviri işi açılmaz. Nedeni: DL7B aynı resx dosyalarına dokunuyor; iki işin aynı dosyalarda ayrı ayrı yürümesi çakışma doğurur. |
| C-3 | DL7B bitiş ölçütüne şu eklenir: altı dilde hiçbir değer en'deki değerle aynı olmaz. Dilden bağımsız olduğu gerekçesiyle aynı kalan değerler (örneğin "ID", sayı biçimleri) prompt'ta adıyla listelenir ve test istisnasına yazılır. Bunun dışında aynı kalan her değer kırmızıdır. Bu kuralı ölçen bir test eklenir ve sabotajla kanıtlanır. |
| C-4 | Çeviri metni `l10n-agent` kurallarına uyar. Ortak `SharedResource` dosyalarına dokunulmaz; çeviri yalnız sayfaya özel `CaseIntakeTriageIndex.*.resx` dosyalarında yapılır. |

## Gerileme değerlendirmesi

- DL7B'nin kapsamı büyür, risk sınıfı değişmez (HIGH).
- DL7B bitene kadar ekran altı dilde İngilizce metin gösterir. Ekranı yetki verilmeden kimse açamadığı için bunun kullanıcıya bugün bir etkisi yok.
- Paket metni değişmez. AC-15 zaten yedi dili istiyor; C-3 bu ölçütün `localization-standard` ile uyumlu ölçümüdür. Paket revizyonu gerekmez (kart S-86).
