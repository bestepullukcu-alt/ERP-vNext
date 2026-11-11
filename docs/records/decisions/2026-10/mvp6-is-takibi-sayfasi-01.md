---
decision_id: MVP6-IS-TAKIBI-SAYFASI-01
status: recorded — sayfa 2026-10-22'de açıldı; bu kayıt onay, kapsam ya da dağıtım izni değildir
decided_at_local: 2026-10-22
decided_by: Natig (yönetim) — açılış onayı MVP-6 Control Tower sohbetinde ("do next", sayfa-ac önerisine cevap)
rule: SOP §22.2 CT-TRK-01, madde 2 (S-39, her CT için zorunlu), 2a ve 8 (kayıt yeri)
scope: SCE / MVP-6 capability block (kuyruk `sce-mvp6`)
recorded_by: MVP-6 Control Tower (Claude Code oturumu)
---

# MVP-6 Tedarik Zinciri İş Takibi sayfası

- **Bağlantı:** https://claude.ai/artifact/QVdTMH2Xo8kk3WpFJs4o8L
- **Ad:** MVP-6 Tedarik Zinciri İş Takibi
- **Erişim:** özel; açıldığında yalnız sahip açabiliyor. Paylaşım sahibin kararıdır ve yalnız ekip içindir, herkese açık
  yapılmaz (SOP §22.2/6).
- **Biçim:** claude.ai Artifact; işler paylaşılan veritabanında (`db`), `items` koleksiyonu (iş başına bir belge: `section`,
  `code`, `title`, `detail`, `status`, `doneAt`, `owner`, `order`). Sahibin işareti `status`/`doneAt` ve
  `markedOnPage: true` olarak kalır.
- **Sayfa kaynağı:** `~/Projects/ERP-vNext-lanes/tracking/mvp6-is-takibi.html` — depo dışı lane klasörü, git izlemez.
- **İlk yükleme (2026-10-22):** depodaki kayıtlardan (plan v10 §1–§5 ve §4a, `docs/records/decisions/2026-10/`, WP-P1 ve
  DC-02 taslakları, açık PR'lar) 47 iş — 32 açık, 15 bitti — yedi bölümde: Şimdi ve sırada · Karar bekleyenler · Sonraki
  fazlar · Bizim takipler · Başka sahiplerin işi (XMC-001) · Ortak bileşenler · Duraklatılanlar. Bir okuma (açık karar
  bekleyenler) 5 işi döndürdü. Görsel önizleme yapılmadı.

## Kurallar (SOP §22.2'den, bu sayfa için)

- Kaynak depodur; sayfa bir görünümdür. Çelişkide depo kazanır ve CT sayfayı düzeltir.
- Sahibin sayfadaki işareti tek başına karar kaydı değildir. CT bir sonraki oturumda işaretleri (`markedOnPage`) okur ve
  depoya yazmadan önce sahibe sorar.
- CT her kabul, prompt ve karardan sonra sayfayı günceller. Block başına tek sayfa; sonraki oturumlar bu bağlantıyı
  kullanır, yeni sayfa açmaz.
