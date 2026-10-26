---
decision_id: PVG-IS-TAKIBI-SAYFASI-01
status: recorded — sayfa 2026-10-21'de açıldı; bu kayıt onay, kapsam ya da dağıtım izni değildir
decided_at_local: 2026-10-21
decided_by: depo sahibi (rol) — `sayfa-ac` (açılış) ve `bağla` (bu kaydın yazılması), PVG Control Tower sohbetinde
rule: SOP §22.2 CT-TRK-01 (pilot: Pharmacovigilance), madde 2a (S-31) ve 8 (kayıt yeri)
scope: Pharmacovigilance capability block (DCP-004, MVP 1)
recorded_by: PVG Control Tower (Claude Code oturumu)
---

# Pharmacovigilance İş Takibi sayfası

- **Bağlantı:** https://claude.ai/artifact/69FHq9M1iNjgTLE3Wdet1r
- **Ad:** Pharmacovigilance İş Takibi
- **Erişim:** özel. Açıldığında yalnız sahip açabiliyordu; paylaşım sahibin kararıdır ve yalnız ekip içindir, herkese
  açık yapılmaz (SOP §22.2/6).
- **Biçim:** claude.ai Artifact; işaretler paylaşılan veritabanında (`db`), işaretleyenin adı için `user` (profile).
  Veri: `items` koleksiyonu (iş başına bir belge) ve `meta/info` (son güncelleme, dal, HEAD).
- **Sayfa kaynağı:** CT worktree'sinde `.ct-teslim/pvg-is-takibi/index.html` — git izlemez (S-29), depoya girmez.
- **İlk yükleme (2026-10-21):** depodaki kayıtlardan 70 iş — 54 açık, 16 bitti; dal `chore/ct/pvg-ct-01` @ `4f6f2d97c`.
  Yalnız görüntüleme yetkisiyle okuma 70 işi döndürdü; kişi adı ya da e-posta yok. Görsel önizleme açılış oturumunda
  yapılamadı.

## Kurallar (SOP §22.2'den, bu sayfa için)

- Kaynak depodur; sayfa bir görünümdür. Çelişkide depo kazanır ve CT sayfayı düzeltir.
- Sahibin sayfadaki işareti tek başına karar kaydı değildir. CT bir sonraki oturumda işaretleri okur ve depoya yazmadan
  önce sahibe sorar.
- CT her kabul, prompt ve karardan sonra sayfayı günceller. Block başına tek sayfa; sonraki oturumlar bu bağlantıyı
  kullanır, yeni sayfa açmaz.
