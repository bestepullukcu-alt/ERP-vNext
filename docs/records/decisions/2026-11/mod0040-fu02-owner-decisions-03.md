# MOD-0040-FU02 — sahip kararları 03 (2026-11-10)

Paket: `execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md` r12 (draft, LF sha256
`d29f9c5e129fc538816ff3276e9caf6edd64c44d5f3a0bd063c83b207c35ba6d`, commit `36465315c`). Sahip: Beste (K2). Kararlar r12'nin
Bölüm 24.1 açık sorularını kapatır; paketin bir sonraki revizyonu (inceleme bulgularıyla birlikte) bunları "karar" olarak işler.

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| Q-1 | Bugün elle girilmiş ORG / POS kodları | **A** — korunur (RCS-001 §2); çakışma savunması paketteki gibi (silinmişler dahil arama, en çok 3 atlama, sonra 409) | Beste | 2026-11-10 |
| Q-2 | Güncellemede kod | **A** — hiçbir kod değişmez, eski elle kodlar dahil (kullanıcı için davranış değişikliği; sürüm notuna yazılır) | Beste | 2026-11-10 |
| Q-3 | Ekran dilleri | **A** — 7 dil (formlar kiracı kabuğunda). CT talimatındaki "2 dil" CT'nin hatasıydı | Beste | 2026-11-10 |
| Q-4 | Tüzel kişilik onay ön kontrolü | **A** — MDM lookup-validation v1.1'e kadar yalnız aktiflik; adlı istisna | Beste | 2026-11-10 |
| Q-5 | POS'un şirketi | **Teyit** — birimin oluşturma anındaki `LegalEntityId`'si | Beste | 2026-11-10 |
| Q-6 | Künye ve onay sırası (S-49) | **A** — bağımsız inceleme sonrası künye `ready-for-dev`'e çevrilir; onay o sürümün sha256'sına yazılır (CT kararı, sahip onayladı) | MVP-1 CT + Beste | 2026-11-10 |
| Q-7 | XMC-001: MOD-0288 dosyaları | **Onay** — yalnız r12 Bölüm 5'te "DL-2" XMC satırlarında listelenen MOD-0288 dosyaları; başka dosya yok. Onaylayan merkez sahibi (Beste) | Beste | 2026-11-10 |
