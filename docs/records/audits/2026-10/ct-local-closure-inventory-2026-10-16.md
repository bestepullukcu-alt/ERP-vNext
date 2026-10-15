# Control Tower işleri — yerel kapanış envanteri (2026-10-16)

Dal `chore/ct/pqg-01`, yalnız yerelde. Yerel `origin/main` `3912d568b`'ye göre 18 commit önde, 29 commit geride (birleşme
tabanı `6b8a62d5a`); 86 dosya. Uzak depo okunmadı (fetch yapılmadı), push / PR / merge yapılmadı.

Yetki: sahibin Codex aracılığıyla verdiği ortak yerel karar yetkisi (`CT-COMPLETION-DELEGATION-OWNER-DECISION-01`).
Aşağıdaki "Codex yerel teknik kabulü" sahip kabulü değildir.

## 1. Tamamlananlar

| İş | Commit | Ne | Durum |
|---|---|---|---|
| Adım 1 — birleşme öncesi düzeltmeler | `47059c694` · `0b392cb9e` | bilinen kırmızılar, hazırlık/yürürlük, katı kuyruk | sahip kabul etti (2026-10-15) |
| Adım 2 — yeni Control Tower açılış paketi + dar İ3 | `543f6ba74` · `171e949f3` · `b5ebb1d96` · `2481004b1` | kart §0, sop7 sözlüğü, keşif aracı | Codex yerel teknik incelemesi engelleyici bulgusuz kapandı; **sahip kabulü bekliyor** |
| CT-PQG-01 — prompt kalite kapısı | `9e7297000` · `168a46142` | `scripts/prompt_gate.py`; SOP §20.2, §36.1 | Codex yerel teknik kabulü |
| CT-OPS-03 — kabul ve kanıt sözleşmesi | `7597f364c` | `scripts/acceptance_record.py`; REP-001 §6.1–6.2; SOP §29.5 | Codex yerel teknik kabulü |
| CT-OPS-04 — durum raporu yeni kip | `5ecd8b3c1` | `scripts/status_report_v2.py`; REP-001 §7.1 | Codex yerel teknik kabulü |
| CT-PILOT-01 — tek sentetik yaşam döngüsü | `f324e8711` | izole klonda 29/29; REP-001 §7.1'e özet komutu | Codex yerel teknik kabulü |
| İ2 (dar) — liste ekranı kancası | `afb5beeca` | "sapma yok" yalnız temiz sinyalde | Codex yerel teknik kabulü |

Her işin kanıtı `docs/records/audits/2026-10/<iş>/`, kararı `docs/records/decisions/2026-10/` altındadır.

## 2. Ertelenenler — iptal değil

Kapsam önerileri sahip kararındaki tablodan (`ct-new-ct-standards-owner-decision-01.md` §1); iş başladığında yeniden
ölçülür.

| İş | Kalan | Gelecekte gerileme riski |
|---|---|---|
| İ1-B | İ1'in kritik olmayan talimat düzeltmeleri | 🟢 düşük — yalnız talimat metni |
| İ2 (geniş) | `/add-module` "başa dön" sınırı; denetim testi ve tarayıcı kontrolünün dört akışta koşması; temiz kod betiklerinin başka yığınlara ait olması | 🟡 orta — ajan döngüleri zaman ve token harcar; veri riski yok |
| İ3 (kalan) | dar İ3 adım 2'de yapıldı; Blueprint sütunları, `SoR_Map` ve diğer maddeler yeniden ölçülmeli | 🟡 orta — mükerrer geliştirme yeniden ortaya çıkabilir |
| İ4 | tek "done" tanımı, paket şablonunda "Teslimatlar", kayıt kodu kilidi (kabul listesi biçimi CT-OPS-03 §6.2 ile geldi) | 🟡 orta — farklı "done" anlamları raporları karıştırır |
| Doğrulayıcı erken dönüşü | `verify_datatable_page.py:953-956` temel dosya eksikken `--format gaps` yerine tam rapor basıyor; 152 liste ekranının 64'ü kancadan gap listesi yerine "doğrulanamadı" alıyor | 🟡 orta — artık sahte yeşil yok, ama ajan listeyi elle almalı |

## 3. Açık sınırlar ve sahip kararları

- **Yayın:** push, PR, merge ve görünürlük değişikliği yapılmadı; depo herkese açık. Hepsi sahip kararıdır.
- **CI ve uzak `main`:** CI koşmadı; uzak durum okunmadı. Mimari takım her işte yerelde bir kez koştu: 131/138, yalnız
  bilinen 7.
- **`.antigravity` teyidi:** zincir yedi dosyayı değiştiriyor — `agents/orchestrator.md`, `agents/product-owner.md`,
  `rules/status-reporting-and-evidence.md` (REP-001), `scripts/list_screen_touch_hook.py`, `scripts/verify_module_id.py`,
  `workflows/add-module.md`, `workflows/release-checklist.md`. Kapanış Control Tower'ı her biri için tek-yazar onayı
  verdi; bu sahip teyidi değildir, birleştirmeden önce sahip teyidi gerekir.
- **REP-001 §7.1 notu:** CT-OPS-04 sırasında eklenen iki cümleyi kapanış Control Tower'ı onay kapsamı dışında sayıyor;
  Codex onları açıkça istediğini ve ortak yetkiyle onayladığını yazıyor. İki görüş
  `ct-i2-list-hook-joint-decision-01.md`'de; karar sahibin.
- **Önceki kanıt sınırları:** önceki işlerin E0 olarak kalan ve "doğrulanmadı" diye yazılan maddeleri kendi
  kayıtlarında açıktır; bu envanter onları kapatmaz.
- **Süreç sapması:** R8 geçişi (`CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01`) yürürlükte.
