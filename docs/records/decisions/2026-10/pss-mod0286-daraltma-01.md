---
record_id: PSS-MOD0286-DARALTMA-01
status: decided — sahip kararı "daralt" (2026-10-26, Kurulum CT üzerinden iletildi); uygulama sırası `daralt-rev09` (Kurulum CT önerdi, sahip bu sohbette teyit etti, 2026-10-26)
prepared_at_local: 2026-10-26
prepared_by: PSS-UI Control Tower (Claude Code oturumu 05ce4e4b)
block: MOD-0286 UI Design System & Page Pattern Governance
pack: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md (rev 08 ebaf59e0… → rev 09)
queue: pss-ui
---

# MOD-0286 — kapsam daraltma (P1 bitiş çizgisi CI'da; P2 kapsam dışı)

## Karar

1. **P1 bitiş çizgisi:** "DL-2 kontrol listesi yazıldı" değil; DL-2'nin otomatik maddeleri yeni ve değişen sayfalar için
   CI'da koşar. Eski sayfalar bugünkü kırmızılarıyla taban olarak kaydedilir ve CI'ı durdurmaz. Bunun için paket rev 09'da
   DL-3 "UI uyum CI bağlantısı" teslim dilimi ve AC-17…AC-20 tanımlanır.
2. **P2 kapsamdan çıkar** (beş yönetim sayfası, `UiPagePattern` ve bağlı API/izin/yönlendirme taslakları). İlk gerçek
   tüketici (MOD-0295 ya da MOD-0297) geldiğinde ayrı bir paketle açılır. D-4 bu kararla kapanır.
3. **DL-1 kısa bir dizin olarak kalır.** Kural çelişkileri D-8a, D-8b, D-8c kuralın kendisinde düzeltilir: `.antigravity/rules`
   altında, sahip onayıyla, ayrı bir iş. MOD-0286 paketi kural dosyasına dokunmaz.
4. **Yeni açık karar D-9:** P1 kabulünden sonra (a) golden paketleri (DEV-0000, DEV-0001) UI kurallarını şartnameye atıfla
   verir, tekrar yazmaz; (b) `.antigravity/agents/` altındaki UI kural kopyaları (ölçüm 2026-10-26:
   `frontend-ui-ux.md:66-67`) çıkarılır, yerine "giriş noktası MOD-0286 + bitirmeden UIC otomatik maddelerini koş ve
   raporla" girer. İkisi de XMC-001 ve `.antigravity` onayıyla ayrı işler.
5. **Taban gizlemez:** Golden Slim'in kendi kırmızısı (FU-4) ve FU-12 tabana adıyla yazılır; Golden Compact (DEV-0001
   `status: in-progress`) tabanda "hazır değil" satırıyla, sahibi developer-enablement.

## Kurulum CT notları (bağlayıcı)

- DL-3 sahipliği: **kod: PSS-UI CT; workflow dosyası (`.github/workflows/…`): CT-Setup + sahip onayı (CODEOWNERS).**
- Taban dosyasında her satır FU kimliği ve sahibini taşır; tabana satır eklemek sahip kararıdır (AC-19).

## Uygulama sırası

Rev 08 onaylanmaz (Codex sonucu gelirse yalnız bilgi olarak kaydedilir, onay satırı açılmaz). Rev 09 = daraltma; tek
bağımsız içerik incelemesi; inceleme ve onay satırları dalın kendi çizgisinde yazılır (S-42: main gerekiyorsa yalnız
dal-birinci-ebeveyn merge, satırlar en sonda); push ve PR sahibin sözüyle, bestepullukcu-alt merge eder. Kuyruk: P1-03
(DL-1 + DL-2 kabulü) PLANNED kalır; DL-3 için yeni iş paketi rev 09 onayından sonra açılır.

## Ölçümler (2026-10-26, main ded2a4de0)

- CI: `.github/workflows/` altında `phase1-gates.yml`, `service-tests.yml`, `agent-adapters.yml`; hiçbiri UI kontrolü
  koşmuyor; main'de son `phase1-gates` ve `service-tests` koşuları başarısız.
- DL-2 betikleri (`uic-auto.sh`) sabit fikstürlere bağlı (Golden Slim, `Platform/Tenants`); değişen sayfa seçicisi yok.
- DEV-0000 `ready-for-dev`, DEV-0001 `in-progress`.
- Rev 07 (`ffeaae57…`) onayı main'de geçersiz (main-ilk ebeveyn birleştirmesi, `main_merge` "iç içe ya da ters merge");
  rev 06 onayı geçerli.
