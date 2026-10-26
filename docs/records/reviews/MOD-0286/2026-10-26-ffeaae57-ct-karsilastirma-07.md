# MOD-0286 — Control Tower karşılaştırması 07 (Codex inceleme 07, SOP §20.4-4)

- Hedef: paket rev 07 @ ecbed8701 (sha256 `ffeaae57…`). Onaylı taban rev 06 (`7f6868d9…`, main'de onaylı, PR #210). İnceleme: Codex, **KOŞULLU**, 1 bulgu (F-18, ORTA), ENGEL yok. Bulgu dosyası `2026-10-26-ffeaae57.md` (Codex'in metni olduğu gibi; `parse_findings` kuru koşusu: 1 bulgu, sorun 0).
- Beyan: model "GPT-6 tabanlı Codex", effort ve oturum "verilemiyor" (yedinci kez); sahip kararı `beyan-yok` uygulanır.

## CT karşılaştırması

- **F-18 — kabul, CT'nin kanıt eksiği.** Paket `:58`, `:517`, `:530` BL-440 için `product-backlog.md:6264` atfını taşıyor; ZIP'teki `excerpts/product-backlog-excerpts.txt` BL-440 kesiti yalnız `:6199-6223` aralığını içeriyordu ve tam dosya ZIP'te yoktu. Paket metni doğru: CT ölçümü (ecbed8701) `product-backlog.md:6264` = "**Paket 2 notları (2026-09-23, WP-UI-LIST-FACTORY-01, merge e9e00106e):** `DitenDataTable.createList` (dataMode fail-closed, filters, …". Bulgu paketi değil kanıt paketini düzeltir; paket değişmez. Kapanış inceleyene kanıt sunularak yapılır (bkz. Sonraki adım).
- **Fark sayımı tutuyor:** inceleyen 17 eklenen / 16 çıkan satır saydı; CT `git diff --numstat` rev 06→07 aynı (17 / 16). AC 16, RC 24 değişmedi; UIC 32 tekil, hepsi 6 hücre; yalnız UIC-4/11/13/15 değişti.
- **Künye görüşü CT'ninkiyle aynı:** `status: ready-for-dev` standarda ve kapı kuralına (`ct_queue.py:1307`) uygun; kapalı build/runtime kapılarıyla çelişmez; `approval_status` satırının kaldırılması onay zorunluluğunu kaldırmaz.
- **P1-03 notları (paket bulgusu değil) — CT doğruladı:** `spec.md:116` premium modal için "dört satır" diyor (paket artık 8); `spec.md:190` çatışma kaydı satırı 2 paketin eski atfını açıklıyor; `spec.md:192` satır 4'te D-5'in iki ek geçişi yok; `compliance-checklist.md:29` UIC-15 satırı 7 hücreye bölünüyor (paket rev 07'de 6). Dördü PSS-UI-P1-03 işinin kapsamına girer (izinli yol `docs/reference/modules/platform/ui-design-system-page-pattern-governance/**`).
- İtiraz yok. Kaçırılan yok.

## Sonraki adım

Seçenekler ve öneri sahibe sunuldu: (a) F-18'i kapatmak için aynı sürüme (`ffeaae57…`) eksik kesiti içeren kısa bir tamamlayıcı Codex incelemesi (07b); paket değişmez, beklenen sonuç UYGUN; (b) F-18 açıkken onay satırı — onay aracı açık ORTA bulguyu durdurmaz (`pack_approval.py` yalnız açık ENGEL'de durur), ama bulgu açık kalır. Kayıt sırası her iki yolda: bu kayıtlar commit → `review-row` 07 (`--ref` o commit) → (07b) → `check` → tek onay satırı (`--author GMG-ERP --approver bestepullukcu-alt`) → push ve PR sahibin sözüyle, bestepullukcu-alt merge eder.
